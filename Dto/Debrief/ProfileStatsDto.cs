namespace Backend.Dto.Debrief;

public record ProfileStatsDto(
    string Profile,
    int PlayerCount,
    double AvgScore,
    double AvgAccuracy,
    IEnumerable<TopMistakeDto> TopMistakes
);