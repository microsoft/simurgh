#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using ChatApp.Server.Models;
using ChatApp.Server.Services;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using System.ComponentModel;

namespace ChatApp.Server.Plugins;

public class VectorSearchPlugin
{
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly IChatCompletionService _chatService;
    private readonly SurveyService _surveyService;
    public VectorSearchPlugin(ITextEmbeddingGenerationService embeddingService, IChatCompletionService chatService, SurveyService surveyService)
    {
        _embeddingService = embeddingService;
        _chatService = chatService;
        _surveyService = surveyService;
    }

    [KernelFunction(nameof(VectorSearchTextAsync))]
    [Description("Perform a vector search of text answers to the survey.")]
    [return: Description("List of answers ranked by cosine and their IDs")]
    public async Task<List<VectorSearchResult>> VectorSearchTextAsync([Description("User question about survey data")] string userQuestion, [Description("Options to additionally filter and sort data. SurveyId is required. TopK is also required.")] VectorSearchOptions vectorSearchOptions)
    {
        var embedding = await _embeddingService.GenerateEmbeddingAsync(userQuestion);

        return await _surveyService.VectorSearchAsync(embedding, vectorSearchOptions);
    }
}
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
