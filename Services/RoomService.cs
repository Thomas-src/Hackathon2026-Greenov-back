using Backend.Utils;

namespace Backend.Services;

public class RoomService
{
    private readonly Dictionary<string, string> _hosts = new();

    public string CreateRoom(string hostConnectionId)
    {
        var roomId = RoomIdGenerator.Generate();
        _hosts[roomId] = hostConnectionId;
        return roomId;
    }

    public bool IsHost(string roomId, string connectionId)
    {
        return _hosts.TryGetValue(roomId, out var host)
               && host == connectionId;
    }
}