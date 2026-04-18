namespace Backend.Dto.Debrief;

public record LeaderboardEntryDto(
    int Rank,
    string Username,
    string Profile,
    int Score
);