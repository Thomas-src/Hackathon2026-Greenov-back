namespace Backend.Dto.Debrief;

public record GlobalDebriefDto(
    string Reason,
    double GlobalCarbon,
    IEnumerable<LeaderboardEntryDto> Leaderboard,
    IEnumerable<ProfileStatsDto> StatsByProfile
);