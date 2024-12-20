namespace ChatApp.Server.Models;

// todo: create models library for shared models and magic strings and enums
public class VectorSearchResult
{
    public VectorSearchResult() { }

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SurveyResponseId { get; set; } = Guid.NewGuid();
    public Guid SurveyQuestionId { get; set; } = Guid.NewGuid();
    public string? TextAnswer { get; set; }
    public double? PositiveSentimentConfidenceScore { get; set; }
    public double? NeutralSentimentConfidenceScore { get; set; }
    public double? NegativeSentimentConfidenceScore { get; set; }
}
