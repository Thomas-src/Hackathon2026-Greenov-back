namespace Backend.Dto.LifeCycle;

public record GameStartedPlayerDto(
    int HouseState,
    double Carbon,
    string Profile,
    string Username
);