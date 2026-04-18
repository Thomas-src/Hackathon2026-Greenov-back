namespace Backend.Domains;

public record ActionResult(
    string ActionId,
    bool IsGoodMove,          // ← remplace Feedback string
    bool PrerequisitesMet,
    int ScoreApplied,
    int CarbonApplied
);