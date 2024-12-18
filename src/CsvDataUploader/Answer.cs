public class Answer
{
    public Answer() { }
    public Answer(Guid surveyId, Guid surveyResponseId, Guid questionId, string? textAnswer = null, decimal? numericAnswer = null)
    {
        SurveyId = surveyId;
        SurveyResponseId = surveyResponseId;
        QuestionId = questionId;
        TextAnswer = textAnswer;
        NumericAnswer = numericAnswer;
    }

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SurveyId { get; set; } = Guid.NewGuid();
    public Guid SurveyResponseId { get; set; } = Guid.NewGuid();
    public Guid QuestionId { get; set; } = Guid.NewGuid();
    public string? TextAnswer { get; set; }
    public decimal? NumericAnswer { get; set; }
    public string? SentimentAnalysisJson { get; set; }
}
