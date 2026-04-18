namespace Backend.Dto.Room;

public record RoomCreatedDto(
    string RoomId,
    int DurationMinutes,
    int TotalEvents
);