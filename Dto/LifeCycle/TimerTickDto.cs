namespace Backend.Dto.LifeCycle;

public record TimerTickDto(
    int Remaining,
    int Total
);