namespace ChatApp.Server.Models;

public class SurveyQuestion
{
    public Guid? Id { get; set; }
    public Guid SurveyId { get; set; }
    public string? Question { get; set; }
    // todo: make enum
    public string? DataType { get; set; }
    public string? Description { get; set; }
}
