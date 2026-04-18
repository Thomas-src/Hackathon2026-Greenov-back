namespace Backend.Domains;

public record ActionResult(
    string ActionId,
    bool IsGoodMove,
    bool PrerequisitesMet,
    int ScoreApplied,
    int CarbonApplied
);