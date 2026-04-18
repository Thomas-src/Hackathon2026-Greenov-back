namespace Backend.Domains;

// Domains/RoomManager.cs
public class RoomManager
{
    private readonly Dictionary<string, Room> _rooms = new();

    // ── Rooms ──────────────────────────────────────────────
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

    public bool IsHost(string roomId, string connectionId)
    {
        var room = GetRoom(roomId);
        return room != null && room.HostConnectionId == connectionId;
    }

    // ── Players ────────────────────────────────────────────
    public void AddPlayer(string roomId, string connectionId, string username, string profile = "")
    {
        var room = GetRoom(roomId);
        if (room == null) return;

        room.Players[connectionId] = new Player
        {
            ConnectionId = connectionId,
            Username = username,
            Profile = profile,
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

    // ── Events ─────────────────────────────────────────────
    public void SetActiveEvent(string roomId, GameEvent evt)
    {
        var room = GetRoom(roomId);
        if (room != null) room.ActiveEvent = evt;
    }

    public GameEvent? GetActiveEvent(string roomId) =>
        GetRoom(roomId)?.ActiveEvent;

    // ── Action history ─────────────────────────────────────
    public List<string> GetPlayerActionHistory(string roomId, string connectionId, string eventId)
    {
        var room = GetRoom(roomId);
        if (room == null || !room.Players.TryGetValue(connectionId, out var player))
            return [];

        return player.ActionHistory
            .Where(r => r.EventId == eventId)
            .Select(r => r.ActionId)
            .ToList();
    }

    public void RecordAction(string roomId, string connectionId, GameEvent evt, GameAction action, ActionResult result)
    {
        var room = GetRoom(roomId);
        if (room == null || !room.Players.TryGetValue(connectionId, out var player)) return;

        player.ActionHistory.Add(new PlayerActionRecord
        {
            EventId = evt.Id,
            EventTitle = evt.Title,
            ActionId = action.Id,
            ActionLabel = action.Label,
            IsGoodMove = result.IsGoodMove,
            PrerequisitesMet = result.PrerequisitesMet,
            ScoreApplied = result.ScoreApplied,
            CarbonApplied = result.CarbonApplied,
            EducationalNote = evt.EducationalNote,
        });
    }

    // ── Scores & carbon ────────────────────────────────────
    public void ApplyScore(string roomId, string connectionId, int delta)
    {
        var room = GetRoom(roomId);
        if (room == null || !room.Players.TryGetValue(connectionId, out var player)) return;
        player.Score += delta;
    }

    public void ApplyCarbonGlobal(string roomId, int delta)
    {
        var room = GetRoom(roomId);
        if (room != null) room.GlobalCarbon += delta;
    }

    public int GetScore(string roomId, string connectionId)
    {
        var room = GetRoom(roomId);
        if (room == null || !room.Players.TryGetValue(connectionId, out var player)) return 0;
        return player.Score;
    }

    public int GetGlobalCarbon(string roomId) =>
        GetRoom(roomId)?.GlobalCarbon ?? 0;

    // ── Bilan ──────────────────────────────────────────────
    public List<PlayerActionRecord> GetPlayerHistory(string roomId, string connectionId)
    {
        var room = GetRoom(roomId);
        if (room == null || !room.Players.TryGetValue(connectionId, out var player)) return [];
        return player.ActionHistory;
    }

    public List<Player> GetAllPlayers(string roomId) =>
        GetRoom(roomId)?.Players.Values.ToList() ?? [];
}