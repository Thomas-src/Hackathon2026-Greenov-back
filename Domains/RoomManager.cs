namespace Backend.Domains;

public class RoomManager
{
    private readonly Dictionary<string, Room> _rooms = new();

    public Room CreateRoom(string hostConnectionId, string roomId)
    {
        var room = new Room
        {
            RoomId = roomId,
            HostConnectionId = hostConnectionId
        };

        _rooms[roomId] = room;
        return room;
    }

    public Room? GetRoom(string roomId) => _rooms.GetValueOrDefault(roomId);

    public void AddPlayer(string roomId, string connectionId, string username)
    {
        var room = GetRoom(roomId);
        if (room == null) return;

        room.Players[connectionId] = new Player
        {
            ConnectionId = connectionId,
            Username = username,
        };
    }

    public void RemovePlayer(string connectionId)
    {
        foreach (var room in _rooms.Values)
        {
            if (room.Players.ContainsKey(connectionId))
            {
                room.Players.Remove(connectionId);
                return;
            }
        }
    }

    public bool IsHost(string roomId, string connectionId)
    {
        var room = GetRoom(roomId);
        return room != null && room.HostConnectionId == connectionId;
    }
}