namespace Backend.Dto.LifeCycle;

public record PlayerSummaryDto(
    string ConnectionId,
    string Username,
    string Profile,
    int Score,
    int Carbone
);