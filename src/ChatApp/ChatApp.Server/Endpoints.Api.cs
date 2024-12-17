
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

        app.MapPost("/conversation", async ([FromServices] ChatCompletionService chat, [FromBody] ConversationRequest history) =>
            new ChatCompletion
            {
                Id = Guid.NewGuid().ToString(),
                ApimRequestId = Guid.NewGuid().ToString(),
                Created = DateTime.UtcNow,
                Choices = [new() {
                Messages = [.. await chat.CompleteChatAsync([.. history.Messages])]
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

        return app;
    }
}
