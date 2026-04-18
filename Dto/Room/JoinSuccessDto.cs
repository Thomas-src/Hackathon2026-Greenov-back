namespace Backend.Dto.Room;

public record JoinSuccessDto(
    string Username,
    string Profile,
    int PlayerCount
);