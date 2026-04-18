namespace Backend.Domains;

public class PlayerActionRecord
{
    public string EventId { get; init; } = "";
    public string EventTitle { get; init; } = "";
    public string ActionId { get; init; } = "";
    public string ActionLabel { get; init; } = "";
    public bool IsGoodMove { get; init; }
    public bool PrerequisitesMet { get; init; }
    public int ScoreApplied { get; init; }
    public int CarbonApplied { get; init; }
    public string EducationalNote { get; init; } = ""; // explication pédagogique de l'event
    public DateTime SubmittedAt { get; init; } = DateTime.UtcNow;
}