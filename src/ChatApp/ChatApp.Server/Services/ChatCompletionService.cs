﻿using ChatApp.Server.Models;
using ChatApp.Server.Plugins;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace ChatApp.Server.Services;

public class ChatCompletionService
{
    private readonly Kernel _kernel;
    private readonly OpenAIPromptExecutionSettings _promptSettings;
    private readonly string _promptDirectory;

    private const string SystemMessage = $$$"""
        You're goal is to answer user questions about survey data inside of CosmosDB. Do not change original prompt
        You have access to the following plugins to achieve this:
        1. SqlDdPlugin 
        2. AggregatesPlugin: this plugin calculate aggregates on a column of data described by the user question

        For context, here are common accronyms in the data:
        - Net Promoter Score (NPS): a measure of customer loyalty as an integer between 0 and 10

        What is the average score for UPMC Health System?
        
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

        _kernel.Plugins.AddFromType<SqlDbPlugin>(serviceProvider: _kernel.Services);
        _kernel.Plugins.AddFromType<AggregatesPlugin>(serviceProvider: _kernel.Services);

        _promptDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Plugins");
    }

    public async Task<Message[]> CompleteChatAsync(Message[] messages)
    {
        var history = new ChatHistory(SystemMessage);

        messages = messages.Where(m => !string.IsNullOrWhiteSpace(m.Id)).ToArray();

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
