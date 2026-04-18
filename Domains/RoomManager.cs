namespace Backend.Domains;

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

    public void ConfigureRoom(string roomId, int durationMinutes, List<GameEvent> eventQueue)
    {
        var room = GetRoom(roomId);
        if (room == null) return;

        room.DurationMinutes = durationMinutes;
        room.TotalEvents = (durationMinutes * 60) / 20;
        room.EventQueue = eventQueue.Take(room.TotalEvents).ToList();
    }

    public void StartRoom(string roomId)
    {
        var room = GetRoom(roomId);
        if (room == null) return;
        room.IsStarted = true;
        room.StartedAt = DateTime.UtcNow;
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

    public List<Player> GetAllPlayers(string roomId) =>
        GetRoom(roomId)?.Players.Values.ToList() ?? [];

    // ── Progression par joueur ─────────────────────────────

    public GameEvent? GetNextEventForPlayer(string roomId, string connectionId, string profile)
    {
        var room = GetRoom(roomId);
        if (room == null) return null;

        room.PlayerEventIndex.TryGetValue(connectionId, out var index);

        // Cherche le prochain event compatible avec le profil à partir de l'index actuel
        var eligibleEvents = room.EventQueue
            .Where(e => string.IsNullOrEmpty(e.ProfileFilter) || e.ProfileFilter == profile)
            .ToList();

        if (index >= eligibleEvents.Count)
            return null;

        var evt = eligibleEvents[index];
        room.PlayerEventIndex[connectionId] = index + 1;
        return evt;
    }

    public bool HasMoreEventsForPlayer(string roomId, string connectionId, string profile)
    {
        var room = GetRoom(roomId);
        if (room == null) return false;

        room.PlayerEventIndex.TryGetValue(connectionId, out var index);

        var eligibleCount = room.EventQueue
            .Count(e => string.IsNullOrEmpty(e.ProfileFilter) || e.ProfileFilter == profile);

        return index < eligibleCount;
    }

    public bool AllPlayersFinished(string roomId)
    {
        var room = GetRoom(roomId);
        if (room == null) return false;

        return room.Players.Values.All(player =>
        {
            room.PlayerEventIndex.TryGetValue(player.ConnectionId, out var index);

            var eligibleCount = room.EventQueue
                .Count(e => string.IsNullOrEmpty(e.ProfileFilter) || e.ProfileFilter == player.Profile);

            return index >= eligibleCount;
        });
    }

// Récupère l'event courant du joueur (après GetNextEventForPlayer, index déjà avancé)
    public GameEvent? GetCurrentEventForPlayer(string roomId, string connectionId, string profile)
    {
        var room = GetRoom(roomId);
        if (room == null) return null;

        room.PlayerEventIndex.TryGetValue(connectionId, out var index);
        var currentIndex = index - 1;

        var eligibleEvents = room.EventQueue
            .Where(e => string.IsNullOrEmpty(e.ProfileFilter) || e.ProfileFilter == profile)
            .ToList();

        if (currentIndex < 0 || currentIndex >= eligibleEvents.Count)
            return null;

        return eligibleEvents[currentIndex];
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