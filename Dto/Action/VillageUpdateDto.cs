namespace Backend.Dto.Action;

public record VillageUpdateDto(
    string ConnectionId,
    string Username,
    string Profile,
    bool IsGoodMove,
    double GlobalCarbon,
    int PlayerScore
);