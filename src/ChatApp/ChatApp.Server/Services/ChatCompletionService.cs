using ChatApp.Server.Models;
using ChatApp.Server.Plugins;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ChatApp.Server.Services;

public class ChatCompletionService
{
    private readonly Kernel _kernel;
    private readonly OpenAIPromptExecutionSettings _promptSettings;
    private readonly string _promptDirectory;

    private const string SystemMessage = $$$"""
        You're goal is to answer user questions about survey data inside of CosmosDB. 


        



        You have access to the following plugins to achieve this:
        1. CosmosDBPlugin: this plugin can fetch columnNames about the container and execute queries
        2. CosmosQueryGeneratorPlugin: this plugin can generate queries based on user input
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
        var history = new ChatHistory(SystemMessage);

        messages = messages.Where(m => !string.IsNullOrWhiteSpace(m.Id)).ToArray();

        // note: that we're not using the chat history in prompting as each question should be considered in isolation
        // chat history is used for generating the title, observability, and suggesting follow-up questions to users
        //var response = await _kernel.GetRequiredService<IChatCompletionService>().GetChatMessageContentAsync(history, _promptSettings, _kernel);
        // commenting out above to see about direct invocation

        try
        {
            // todo: inject the partitionKey viz. filename
            var columnNames = await _kernel.InvokeAsync(nameof(CosmosDBPlugin), nameof(CosmosDBPlugin.GetColumnNamesAsync));

            var promptYaml = File.ReadAllText(Path.Combine(_promptDirectory, "CosmosQueryGenerationPlugin", "QueryGeneration.yaml"));

            var function = _kernel.CreateFunctionFromPromptYaml(promptYaml);

            var columnNamesLiteral = columnNames.GetValue<List<string>>()?
                .Select(s => $"\"{s}\"") ?? [];

            var columnNamesStr = string.Join(",", columnNamesLiteral);

            var cosmosQuery = await _kernel.InvokeAsync(function, new() {
                { "queryintent", messages.Last().Content },
                { "columnnames", columnNamesStr }
            });

            var cosmosQueryResult = await _kernel.InvokeAsync(nameof(CosmosDBPlugin), nameof(CosmosDBPlugin.ExecuteCosmosQueryAsync), new() { { "query", cosmosQuery } });

            // todo: implement retry pattern

        }
        catch (Exception exception)
        {
            Console.WriteLine(exception.Message);
            throw;
        }


        // append response messages to messages array
        var responseMessages = messages.ToList();

        // tool calls and responses are added back to the response messages
        history.Where(m => m.Role == AuthorRole.Tool).ToList().ForEach(item => responseMessages.Add(new Message
        {
            Id = Guid.NewGuid().ToString(),
            Role = AuthorRole.Tool.ToString().ToLower(),
            Content = item.ToString()!,
            Date = DateTime.UtcNow
        }));

        // latest assistant response is added to the response messages
        //response.Items.ToList().ForEach(item => responseMessages.Add(new Message
        //{
        //    Id = Guid.NewGuid().ToString(),
        //    Role = AuthorRole.Assistant.ToString().ToLower(),
        //    Content = item.ToString()!,
        //    Date = DateTime.UtcNow
        //}));

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
