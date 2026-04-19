namespace Backend.Domains;

public class RoomManager(ILogger<RoomManager> logger)
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

    public bool IsRoomEmpty(string roomId) =>
        GetRoom(roomId)?.Players.Count == 0;

    public void DeleteRoom(string roomId) =>
        _rooms.Remove(roomId);

    public Room? GetRoom(string roomId) => _rooms.GetValueOrDefault(roomId);

    public bool IsHost(string roomId, string connectionId)
    {
        var room = GetRoom(roomId);
        return room != null && room.HostConnectionId == connectionId;
    }

    public void ConfigureRoom(string roomId, int durationMinutes, List<GameEvent> eventQueue)
    {
        var room = GetRoom(roomId);
        if (room == null) return;

        var eventsPerProfile = (durationMinutes * 60) / 20;

        var profiles = new[] { "proprietaire", "locataire", "entreprise" };

        // Une queue dédiée par profil
        room.EventQueueByProfile = profiles.ToDictionary(
            profile => profile,
            profile => eventQueue
                .Where(e => e.ProfileFilter?.Equals(profile, StringComparison.InvariantCultureIgnoreCase) == true)
                .OrderBy(_ => Random.Shared.Next())
                .Take(eventsPerProfile)
                .ToList()
        );

        logger.LogInformation(
            "Sample events ProfileFilter: [{Samples}]",
            string.Join(", ", eventQueue.Take(5).Select(e => $"id={e.Id} filter='{e.ProfileFilter}'"))
        );

        logger.LogInformation(
            "EventQueueByProfile keys: [{Keys}], locataire count: {Count}, first ProfileFilter values: [{Filters}]",
            string.Join(", ", room.EventQueueByProfile.Keys),
            room.EventQueueByProfile.TryGetValue("locataire", out var loc) ? loc.Count : -1,
            string.Join(", ", eventQueue.Take(5).Select(e => $"'{e.ProfileFilter}'"))
        );

        room.DurationMinutes = durationMinutes;
        room.TotalEvents = eventsPerProfile;
    }

    public void StartRoom(string roomId)
    {
        var room = GetRoom(roomId);
        if (room == null) return;
        room.IsStarted = true;
        room.StartedAt = DateTime.UtcNow;
    }

    // ── Players ────────────────────────────────────────────

    public void AddPlayer(string roomId, string connectionId, string username, Profile profile)
    {
        var room = GetRoom(roomId);
        if (room == null) return;

        room.Players[connectionId] = new Player
        {
            ConnectionId = connectionId,
            Username = username,
            Profile = profile,
            Score = 0
        };
    }

    public string? GetRoomIdByConnectionId(string connectionId) =>
        _rooms.Values
            .FirstOrDefault(r => r.Players.ContainsKey(connectionId))
            ?.RoomId;

    public Player? RemovePlayer(string connectionId)
    {
        foreach (var room in _rooms.Values.Where(room => room.Players.ContainsKey(connectionId)))
        {
            room.Players.Remove(connectionId, out var value);
            return value;
        }

        return null;
    }

    public List<Player> GetAllPlayers(string roomId) =>
        GetRoom(roomId)?.Players.Values.ToList() ?? [];

    public int Count(string roomId) => GetAllPlayers(roomId).Count;

    // ── Progression par joueur ─────────────────────────────

    public void AdvancePlayerEvent(string roomId, string connectionId)
    {
        var room = GetRoom(roomId);
        if (room == null) return;

        room.PlayerEventIndex.TryGetValue(connectionId, out var index);
        room.PlayerEventIndex[connectionId] = index + 1;
    }

    public GameEvent? PeekNextEventForPlayer(string roomId, string connectionId, string profile)
    {
        var room = GetRoom(roomId);
        if (room == null) return null;

        if (!room.EventQueueByProfile.TryGetValue(profile, out var events)) return null;

        room.PlayerEventIndex.TryGetValue(connectionId, out var index);
        return index < events.Count ? events[index] : null;
    }

    public GameEvent? GetCurrentEventForPlayer(string roomId, string connectionId, string profile)
    {
        var room = GetRoom(roomId);
        if (room == null) return null;

        if (!room.EventQueueByProfile.TryGetValue(profile, out var events)) return null;

        room.PlayerEventIndex.TryGetValue(connectionId, out var index);
        var currentIndex = index - 1;
        return currentIndex >= 0 && currentIndex < events.Count ? events[currentIndex] : null;
    }

    public bool HasMoreEventsForPlayer(string roomId, string connectionId, string profile)
    {
        var room = GetRoom(roomId);
        if (room == null) return false;

        if (!room.EventQueueByProfile.TryGetValue(profile, out var events)) return false;

        room.PlayerEventIndex.TryGetValue(connectionId, out var index);
        return index < events.Count;
    }

    public bool AllPlayersFinished(string roomId)
    {
        var room = GetRoom(roomId);
        if (room == null || room.Players.Count == 0) return false;

        return room.Players.Values.All(player =>
        {
            var profile = player.Profile.ToString().ToLowerInvariant();
            if (!room.EventQueueByProfile.TryGetValue(profile, out var events)) return true;

            room.PlayerEventIndex.TryGetValue(player.ConnectionId, out var index);
            return index >= events.Count;
        });
    }

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

    // ── Timer ──────────────────────────────────────────────

    public void SetTimerCts(string roomId, CancellationTokenSource cts)
    {
        var room = GetRoom(roomId);
        if (room != null) room.TimerCts = cts;
    }

    public void CancelTimer(string roomId)
    {
        var room = GetRoom(roomId);
        room?.TimerCts?.Cancel();
    }
}