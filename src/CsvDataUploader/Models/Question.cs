﻿namespace CsvDataUploader.Models;

public class Question
{
    public Question() { }
    public Question(Guid surveyId, string text, string dataType, string description, ReadOnlyMemory<float>? embedding = null)
    {
        SurveyId = surveyId;
        Text = text;
        DataType = dataType;
        Description = description;
        Embedding = embedding;
    }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SurveyId { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<Answer> Answers { get; set; } = [];
    public ReadOnlyMemory<float>? Embedding { get; set; } = null;
}
