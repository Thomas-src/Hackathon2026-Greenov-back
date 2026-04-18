namespace Backend.Dto.Action;

public record ActionFeedbackDto(
    string ActionId,
    bool IsGoodMove,
    bool PrerequisitesMet,
    int NewScore,
    string EducationalNote,
    bool HasNextEvent
);