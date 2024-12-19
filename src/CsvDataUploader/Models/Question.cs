public class Question
{
    public Question() { }
    public Question(Guid surveyId, string text, string dataType, string description)
    {
        SurveyId = surveyId;
        Text = text;
        DataType = dataType;
        Description = description;
    }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SurveyId { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<Answer> Answers { get; set; } = [];
}
