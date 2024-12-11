using ChatApp.Server.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace ChatApp.Server.Services;

public class ChatCompletionService
{
    private readonly Kernel _kernel;
    private readonly OpenAIPromptExecutionSettings _promptSettings;
    private readonly string _promptDirectory;

    private const string MemberAssistantInstructions = $$$"""
        You're goal is to answer user questions about survey data inside of CosmosDB. Generate the CosmosDB container metadata.
        You have access to the following plugins:
        1. CosmosDBPlugin
        2. TextPlugin
        3. CosmosQueryGeneratorPlugin
        """;

    public ChatCompletionService(Kernel kernel)
    {
        _kernel = kernel;
        _promptSettings = new OpenAIPromptExecutionSettings
        {
            MaxTokens = 1024,
            Temperature = 0.5,
            StopSequences = [],
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };

        _promptDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Plugins");
    }

    public async Task<Message[]> CompleteChatAsync(Message[] messages)
    {
        var history = new ChatHistory(MemberAssistantInstructions);

        messages = messages.Where(m => !string.IsNullOrWhiteSpace(m.Id)).ToArray();

        // filter out 'tool' messages and 'empty' messages, add rest to history
        messages.Where(m => !m.Role.Equals(AuthorRole.Tool.ToString(), StringComparison.OrdinalIgnoreCase))
            .ToList()
            .ForEach(m => history.AddMessage(ParseRole(m.Role), m.Content));

        var response = await _kernel.GetRequiredService<IChatCompletionService>().GetChatMessageContentAsync(history, _promptSettings, _kernel);

        // append response messages to messages array
        var responseMessages = messages.ToList();

        // tool calls and responses are added to the response messages
        history.Where(m => m.Role == AuthorRole.Tool).ToList().ForEach(item => responseMessages.Add(new Message
        {
            Id = Guid.NewGuid().ToString(),
            Role = AuthorRole.Tool.ToString().ToLower(),
            Content = item.ToString()!,
            Date = DateTime.UtcNow
        }));

        response.Items.ToList().ForEach(item => responseMessages.Add(new Message
        {
            Id = Guid.NewGuid().ToString(),
            Role = AuthorRole.Assistant.ToString().ToLower(),
            Content = item.ToString()!,
            Date = DateTime.UtcNow
        }));

        return [.. responseMessages];
    }

    public async Task<string> GenerateTitleAsync(List<Message> messages)
    {
        try
        {
            // Create a conversation string from the messages
            string conversationText = string.Join(" ", messages.Select(m => m.Role + " " + m.Content));

            // Load prompt yaml
            var promptYaml = File.ReadAllText(Path.Combine(_promptDirectory, "TextPlugin", "SummarizeConversation.yaml"));
            var function = _kernel.CreateFunctionFromPromptYaml(promptYaml);

            // Invoke the function against the conversation text
            var result = await _kernel.InvokeAsync(function, new() { { "history", conversationText } });

            var factory = _kernel.Services.GetService<IHttpClientFactory>();

            string completion = result.ToString()!;

            return completion;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            throw;
        }
    }

    internal static AuthorRole ParseRole(string roleName)
    {
        return (roleName.ToLower() ?? string.Empty) switch
        {
            "user" => AuthorRole.User,
            "assistant" => AuthorRole.Assistant,
            "tool" => AuthorRole.Tool,
            "system" => AuthorRole.System,
            _ => AuthorRole.User,
        };
    }
}
