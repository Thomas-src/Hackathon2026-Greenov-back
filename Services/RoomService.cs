namespace Backend.Services;

public class RoomService
{
    private readonly Dictionary<string, string> _hostConnections = new();

    public void SetHost(string roomId, string connectionId)
    {
        _hostConnections[roomId] = connectionId;
    }

    public string? GetHost(string roomId)
    {
        return _hostConnections.GetValueOrDefault(roomId);
    }
}