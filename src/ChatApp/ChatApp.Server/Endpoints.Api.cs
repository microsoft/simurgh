
using ChatApp.Server.Models;
using ChatApp.Server.Models.Options;
using ChatApp.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ChatApp.Server;

public static partial class Endpoints
{
    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        app.MapGet("/frontend_settings", ([FromServices] IOptions<FrontendOptions> settings) => settings.Value)
            .WithName("GetFrontendSettings")
            .WithOpenApi();

        // todo: resolve complet chat endpoint at later date... issue with guid required for surveyid
        app.MapPost("/conversation", async ([FromServices] ChatCompletionService chat, [FromBody] ConversationRequest history) =>
            new ChatCompletion
            {
                Id = Guid.NewGuid().ToString(),
                ApimRequestId = Guid.NewGuid().ToString(),
                Created = DateTime.UtcNow,
                Choices = [new() {
                Messages = [.. await chat.CompleteChatAsync(Guid.NewGuid(), [.. history.Messages])]
            }]
            });

        app.MapGet("/surveys", async ([FromServices] SurveyService surveyService) =>
        {
            var surveys = await surveyService.GetSurveysAsync();

            return Results.Ok(surveys);
        }).WithName("GetSurveys")
        .WithOpenApi();

        app.MapGet("/surveys/{surveyId}/questions", async ([FromServices] SurveyService surveyService, [FromRoute] Guid surveyId) =>
        {
            var surveyQuestions = await surveyService.GetSurveyQuestionsAsync(surveyId);

            return Results.Ok(surveyQuestions);
        }).WithName("GetSurveyQuestions")
        .WithOpenApi();

        app.MapGet("/suggest-questions/{surveyId}", async ([FromServices] ChatCompletionService chat, [FromRoute] string surveyId) =>
        {
            if (!Guid.TryParse(surveyId, out var guid))
                return Results.BadRequest("Invalid surveyId");

            var suggestions = await chat.GenerateSuggestedQuestionsAsync(guid);

            return Results.Ok(suggestions);
        }).WithName("SuggestQuestions");


        app.MapGet("/testing/{surveyId}", async ([FromServices] ChatCompletionService chat, [FromServices] SurveyService surveyService, [FromQuery] string userQuestion, [FromRoute] Guid surveyId) =>
        {
            var embedding = await chat.GetEmbeddingAsync(userQuestion);
            return await surveyService.VectorSearchAsync(surveyId, userQuestion, embedding);
        });

        return app;
    }
}
