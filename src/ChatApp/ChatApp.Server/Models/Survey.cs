namespace ChatApp.Server.Models;

public class Survey
{
    public Guid? Id { get; set; }
    public string? Filename { get; set; }
    public string? Version { get; set; }

    public List<string>? Questions { get; set; } = null;
}
