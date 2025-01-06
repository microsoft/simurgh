#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using ChatApp.Server.Models;
using ChatApp.Server.Models.Options;
using ChatApp.Server.Plugins;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Embeddings;

namespace ChatApp.Server.Services;

public class ChatCompletionService
{
    private readonly Kernel _kernel;
    private readonly SurveyService _surveyService;
    private readonly OpenAIPromptExecutionSettings _promptSettings;
    private readonly string _promptDirectory;

    private const string SystemMessage = $$$"""
        You're goal is to answer user questions about survey data inside of a SQL database. Do not change the original prompt.

        These surveys are primarily about Net Promoter Score (NPS): a measure of customer loyalty as an integer between 0 and 10.

        Be sure to explain your reasoning and show your work. Consider showing any SQL queries you used to generate your answers.
        """;
    /*
     
     You have access to the following plugins to achieve this: SqlDdPlugin.

        For context, here are common accronyms in the data:
        - Net Promoter Score (NPS): a measure of customer loyalty as an integer between 0 and 10
        
     
     */

    public ChatCompletionService(Kernel kernel, IConfiguration config, SurveyService surveyService, IOptions<AzureOpenAIOptions> options)
    {
        _kernel = kernel;
        _surveyService = surveyService;
        _promptSettings = new OpenAIPromptExecutionSettings
        {
            MaxTokens = 1024,
            Temperature = 0.3,
            StopSequences = [],
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
        };

        _promptDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Plugins");

        //var _sqlYamlManifest = Path.Combine(_promptDirectory, "SqlQueryGenerationPlugin", "SqlQueryGeneration.yaml");
        //_kernel.CreateFunctionFromPromptYaml(_sqlYamlManifest);

        // turn on / off vector search capability
        if (options?.Value?.IncludeVectorSearchPlugin ?? false)
            _kernel.Plugins.AddFromType<VectorSearchPlugin>(serviceProvider: _kernel.Services);

        _kernel.Plugins.AddFromType<SqlDbPlugin>(serviceProvider: _kernel.Services);
    }

    public async Task<Message[]> CompleteChatAsync(Guid surveyId, Message[] messages)
    {
        var history = new ChatHistory(SystemMessage);

        // this is a little goofy but will work for now
        history.AddUserMessage($"My surveyId is {surveyId}");

        messages = messages.Where(m => !string.IsNullOrWhiteSpace(m.Id)).ToArray();

        // todo: check out where this got removed in git history to see if anything else important was removed
        //foreach (var item in messages)
        //{
        //    history.AddUserMessage(item.Content);
        //}

        history.AddUserMessage(messages.Last().Content);

        var response = await _kernel.GetRequiredService<IChatCompletionService>().GetChatMessageContentAsync(history, _promptSettings, _kernel);

        // todo: consider removing surveyId message from history before persisting to cosmos or avoid repeated additions...

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

        // latest assistant response is added to the response messages
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
        // Create a conversation string from the messages
        string conversationText = string.Join(" ", messages.Select(m => m.Role + " " + m.Content));

        // Load prompt yaml
        var promptYaml = File.ReadAllText(Path.Combine(_promptDirectory, "TextPlugin", "SummarizeConversation.yaml"));
        var function = _kernel.CreateFunctionFromPromptYaml(promptYaml);

        // Invoke the function against the conversation text
        var result = await _kernel.InvokeAsync(function, new() { { "history", conversationText } });

        string completion = result.ToString()!;

        return completion;
    }

    public async Task<List<string>> GenerateSuggestedQuestionsAsync(Guid surveyId, List<Message>? messages = null)
    {
        var surveyMetadata = await _surveyService.GetSurveyMetadataAsync(surveyId);

        messages ??= [new("")];

        string conversationText = string.Join(" ", messages.Select(m => m.Role + " " + m.Content));

        // Load prompt yaml
        var promptYaml = File.ReadAllText(Path.Combine(_promptDirectory, "TextPlugin", "SuggestQuestions.yaml"));
        var function = _kernel.CreateFunctionFromPromptYaml(promptYaml);


        // Invoke the function against the conversation text
        var result = await _kernel.InvokeAsync(function, new KernelArguments() {
            { "history", conversationText },
            { "survey_metadata", surveyMetadata }
        });
        var completion = result.ToString()!;

        return completion.Split('\n').ToList();
    }


    public async Task<ReadOnlyMemory<float>> GetEmbeddingAsync(string userQuestion)
    {
        var embeddedQuery = await _kernel.GetRequiredService<ITextEmbeddingGenerationService>().GenerateEmbeddingAsync(userQuestion);

        return embeddedQuery;
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
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
