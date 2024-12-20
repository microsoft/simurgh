namespace ChatApp.Server.Models;

// todo: create models library for shared models and magic strings and enums
public class SurveyQuestionAnswer
{
    public SurveyQuestionAnswer() { }
    //public SurveyQuestionAnswer(Guid surveyId, Guid surveyResponseId, Guid questionId, string? textAnswer = null, decimal? numericAnswer = null)
    //{
    //    SurveyId = surveyId;
    //    SurveyResponseId = surveyResponseId;
    //    SurveyQuestionId = questionId;
    //    TextAnswer = textAnswer;
    //    NumericAnswer = numericAnswer;
    //}

    public Guid Id { get; set; } = Guid.NewGuid();
    //public Guid SurveyId { get; set; } = Guid.NewGuid();
    //public Guid SurveyResponseId { get; set; } = Guid.NewGuid();
    //public Guid SurveyQuestionId { get; set; } = Guid.NewGuid();
    public string? TextAnswer { get; set; }
    //public decimal? NumericAnswer { get; set; }
    //public double? PositiveSentimentConfidenceScore { get; set; }
    //public double? NeutralSentimentConfidenceScore { get; set; }
    //public double? NegativeSentimentConfidenceScore { get; set; }

    // temporarily adding properties to test hybrid search code
    public float Score { get; set; }
    public float SemanticRank { get; set; }
    public float KeywordRank { get; set; }
}
