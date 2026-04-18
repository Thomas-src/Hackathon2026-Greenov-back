namespace Backend.Dto.LifeCycle;

public record GameStartedHostDto(
    long EndsAtUnix,
    IEnumerable<PlayerSummaryDto> Players,
    double GlobalCarbon
);
