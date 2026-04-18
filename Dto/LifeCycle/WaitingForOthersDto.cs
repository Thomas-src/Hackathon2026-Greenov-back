namespace Backend.Dto.LifeCycle;

public record WaitingForOthersDto(
    string Message,
    int Score
);