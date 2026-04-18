namespace Backend.Dto.Debrief;

public record MistakeDto(
    string EventTitle,
    string ActionLabel,
    bool PrerequisitesMet,
    string EducationalNote
);