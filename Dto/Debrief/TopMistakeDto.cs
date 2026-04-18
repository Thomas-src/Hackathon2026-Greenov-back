namespace Backend.Dto.Debrief;

public record TopMistakeDto(
    string EventTitle,
    int Count,
    string EducationalNote
);