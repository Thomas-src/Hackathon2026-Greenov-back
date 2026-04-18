namespace Backend.Dto.Debrief;

public record PlayerDebriefDto(
    int TotalScore,
    double TotalCarbon,
    int GoodMoves,
    int BadMoves,
    IEnumerable<MistakeDto> Mistakes
);