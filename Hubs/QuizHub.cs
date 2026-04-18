using Backend.Catalog.Models;
using Backend.Domains;
using Backend.Dto.Action;
using Backend.Dto.Debrief;
using Backend.Dto.Event;
using Backend.Dto.LifeCycle;
using Backend.Dto.Room;
using Backend.Services;
using Backend.Utils;
using Microsoft.AspNetCore.SignalR;

namespace Backend.Hubs;

public class QuizHub(
    RoomManager roomManager,
    ActionEngine actionEngine,
    EventCatalog eventCatalog,
    ILogger<QuizHub> logger)
    : Hub
{
    // ── HOST ───────────────────────────────────────────────────────────────────

    public async Task CreateRoom(int durationMinutes)
    {
        var roomId = RoomIdGenerator.Generate();
        roomManager.CreateRoom(Context.ConnectionId, roomId);
        roomManager.ConfigureRoom(roomId, durationMinutes, eventCatalog.GetAll());

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        var room = roomManager.GetRoom(roomId)!;
        logger.LogInformation("Room {RoomId} créée — {Duration} min — {Total} events",
            roomId, durationMinutes, room.TotalEvents);

        await Clients.Caller.SendAsync("RoomCreated", new RoomCreatedDto(
            RoomId: roomId,
            DurationMinutes: durationMinutes,
            TotalEvents: room.TotalEvents
        ));
    }

    public async Task StartGame(string roomId)
    {
        logger.LogInformation(
            "StartGame — roomId: {RoomId}, caller: {ConnId}, isHost: {IsHost}",
            roomId, Context.ConnectionId, roomManager.IsHost(roomId, Context.ConnectionId));

        if (!roomManager.IsHost(roomId, Context.ConnectionId)) return;

        var room = roomManager.GetRoom(roomId);
        if (room == null || room.IsStarted) return;

        roomManager.StartRoom(roomId);

        var startAt = (room.StartedAt ??= DateTime.UtcNow);
        var endsAt = startAt.AddMinutes(room.DurationMinutes);
        var endsAtUnix = new DateTimeOffset(endsAt).ToUnixTimeSeconds();
        
        logger.LogInformation("EndAt {}: , startAt: {}, time {}", endsAt, startAt, room.DurationMinutes);
        await Clients.Caller.SendAsync("GameStartedHost", new GameStartedHostDto(
            EndsAtUnix: endsAtUnix,
            Players: room.Players.Values.Select(p => new PlayerSummaryDto(
                p.ConnectionId, p.Username, p.Profile.ToString().ToLowerInvariant(), p.Score, p.CarbonLevel)),
            GlobalCarbon: room.GlobalCarbon
        ));

        var cts = new CancellationTokenSource();
        roomManager.SetTimerCts(roomId, cts);
        _ = RunGameTimerAsync(roomId, room.DurationMinutes, cts.Token);

        foreach (var (connectionId, player) in room.Players)
        {
            await Clients.Client(connectionId).SendAsync("GameStartedPlayer", new GameStartedPlayerDto(
                HouseState: player.Score,
                Carbon: player.CarbonLevel,
                Profile: player.Profile.ToString().ToLowerInvariant()
            ), cancellationToken: cts.Token);

            await SendNextEventToPlayer(roomId, connectionId);
        }
    }

    public async Task ForceEndGame(string roomId)
    {
        if (!roomManager.IsHost(roomId, Context.ConnectionId)) return;

        var room = roomManager.GetRoom(roomId);
        if (room is not { IsStarted: true }) return;
        
        await EndGame(roomId, "forced");
    }

    // ── PLAYER ─────────────────────────────────────────────────────────────────

    public async Task JoinRoom(string roomId, string username, string profile)
    {
        var room = roomManager.GetRoom(roomId);

        if (room == null)
        {
            await Clients.Caller.SendAsync("JoinFailed", new JoinFailedDto(
                Message: "Session introuvable. Vérifie le code et réessaie."
            ));
            return;
        }

        if (room.IsStarted)
        {
            await Clients.Caller.SendAsync("JoinFailed", new JoinFailedDto(
                Message: "La partie a déjà commencé."
            ));
            return;
        }

        Enum.TryParse<Profile>(profile, ignoreCase: true, out var profileEnum);

        roomManager.AddPlayer(roomId, Context.ConnectionId, username, profileEnum);
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        logger.LogInformation("Player {Username} ({Profile}) a rejoint {RoomId}",
            username, profile, roomId);

        await Clients.Caller.SendAsync("JoinSuccess", new JoinSuccessDto(
            Username: username,
            Profile: profile,
            PlayerCount: room.Players.Count
        ));

        await Clients.GroupExcept(roomId, Context.ConnectionId).SendAsync("PlayerJoined",
            new PlayerJoinedDto(PlayerCount: room.Players.Count));
    }

    public async Task SubmitAction(string roomId, string actionId)
    {
        var room = roomManager.GetRoom(roomId);
        if (room == null || !room.IsStarted) return;
        if (!room.Players.TryGetValue(Context.ConnectionId, out var player)) return;

        var gameEvent = roomManager.GetCurrentEventForPlayer(roomId, Context.ConnectionId, player.Profile.ToString().ToLowerInvariant());
        logger.LogInformation("SubmitAction — actionId: {ActionId}, gameEvent: {EventId}",
            actionId, gameEvent?.Id ?? "NULL");
        var action = gameEvent?.Actions.FirstOrDefault(a => a.Id == actionId);
        if (action == null || gameEvent == null) return;

        var history = roomManager.GetPlayerActionHistory(roomId, Context.ConnectionId, gameEvent.Id);
        var result = actionEngine.Evaluate(action, history);

        roomManager.RecordAction(roomId, Context.ConnectionId, gameEvent, action, result);
        roomManager.ApplyScore(roomId, Context.ConnectionId, result.ScoreApplied);
        roomManager.ApplyCarbonGlobal(roomId, result.CarbonApplied);

        var newScore = roomManager.GetScore(roomId, Context.ConnectionId);

        await Clients.Client(room.HostConnectionId).SendAsync("VillageUpdate", new VillageUpdateDto(
            ConnectionId: player.ConnectionId,
            Username: player.Username,
            Profile: player.Profile.ToString().ToLowerInvariant(),
            IsGoodMove: result.IsGoodMove,
            GlobalCarbon: roomManager.GetGlobalCarbon(roomId),
            PlayerScore: newScore
        ));

        await Clients.Caller.SendAsync("ActionFeedback", new ActionFeedbackDto(
            ActionId: result.ActionId,
            IsGoodMove: result.IsGoodMove,
            PrerequisitesMet: result.PrerequisitesMet,
            NewScore: newScore,
            EducationalNote: gameEvent.EducationalNote,
            HasNextEvent: roomManager.HasMoreEventsForPlayer(roomId, Context.ConnectionId, player.Profile.ToString().ToLowerInvariant())
        ));

        await SendNextEventToPlayer(roomId, Context.ConnectionId);
    }

    public async Task GetMyDebrief(string roomId)
    {
        var debrief = BuildPlayerDebrief(
            roomManager.GetPlayerHistory(roomId, Context.ConnectionId));

        await Clients.Caller.SendAsync("Debrief", debrief);
    }

    // ── DISCONNECTION ──────────────────────────────────────────────────────────

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var roomId = roomManager.GetRoomIdByConnectionId(Context.ConnectionId);
        var player = roomManager.RemovePlayer(Context.ConnectionId);

        if (roomId != null && player != null)
        {
            await Clients.Group(roomId).SendAsync("PlayerLeft", new PlayerLeftDto(
                Username: player.Username,
                PlayerCount: roomManager.Count(roomId)
            ));

            var room = roomManager.GetRoom(roomId);
            if (room is { IsStarted: true } && roomManager.IsRoomEmpty(roomId))
            {
                roomManager.DeleteRoom(roomId);
                logger.LogInformation("Room {RoomId} supprimée — plus aucun joueur connecté", roomId);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    // ── PRIVATE ────────────────────────────────────────────────────────────────

    private async Task SendNextEventToPlayer(string roomId, string connectionId)
    {
        var room = roomManager.GetRoom(roomId);
        if (room == null) return;
        if (!room.Players.TryGetValue(connectionId, out var player)) return;

        var nextEvent = roomManager.PeekNextEventForPlayer(roomId, connectionId, player.Profile.ToString().ToLowerInvariant());

        if (nextEvent == null)
        {
            await Clients.Client(connectionId).SendAsync("WaitingForOthers", new WaitingForOthersDto(
                Message: "Tu as terminé toutes tes tâches !",
                Score: roomManager.GetScore(roomId, connectionId)
            ));

            if (roomManager.AllPlayersFinished(roomId))
                await EndGame(roomId, "completed");

            return;
        }

        // Avance l'index seulement après avoir confirmé qu'on envoie l'event
        roomManager.AdvancePlayerEvent(roomId, connectionId);

        room.PlayerEventIndex.TryGetValue(connectionId, out var index);

        await Clients.Client(connectionId).SendAsync("NewEvent", new NewEventDto(
            Id: nextEvent.Id,
            Title: nextEvent.Title,
            Description: nextEvent.Description,
            EventNumber: index,
            TotalEvents: room.TotalEvents,
            Actions: nextEvent.Actions.Select(a => new ActionChoiceDto(a.Id, a.Label))
        ));
    }

    private async Task RunGameTimerAsync(string roomId, int durationMinutes, CancellationToken ct)
    {
        var totalSeconds = durationMinutes * 60;
        var elapsed = 0;

        try
        {
            while (elapsed < totalSeconds && !ct.IsCancellationRequested)
            {
                await Task.Delay(1000, ct);
                elapsed++;

                var remaining = totalSeconds - elapsed;

                if (remaining % 5 == 0 || remaining <= 10)
                {
                    await Clients.Group(roomId).SendAsync("TimerTick", new TimerTickDto(
                        Remaining: remaining,
                        Total: totalSeconds
                    ), cancellationToken: ct);
                }
            }

            if (!ct.IsCancellationRequested)
                await EndGame(roomId, "timeout");
        }
        catch (TaskCanceledException) { }
    }

    private async Task EndGame(string roomId, string reason)
    {
        roomManager.CancelTimer(roomId);

        var players = roomManager.GetAllPlayers(roomId);

        foreach (var player in players)
        {
            var debrief = BuildPlayerDebrief(
                roomManager.GetPlayerHistory(roomId, player.ConnectionId));

            await Clients.Client(player.ConnectionId).SendAsync("Debrief", debrief);
        }

        var room = roomManager.GetRoom(roomId);

        var globalDebrief = new GlobalDebriefDto(
            Reason: reason,
            GlobalCarbon: roomManager.GetGlobalCarbon(roomId),
            Leaderboard: players
                .OrderByDescending(p => p.Score)
                .Select((p, i) => new LeaderboardEntryDto(
                    Rank: i + 1,
                    Username: p.Username,
                    Profile: p.Profile.ToString().ToLowerInvariant(),
                    Score: p.Score)),
            StatsByProfile: players
                .GroupBy(p => p.Profile)
                .Select(g => new ProfileStatsDto(
                    Profile: g.Key.ToString().ToLowerInvariant(),
                    PlayerCount: g.Count(),
                    AvgScore: g.Average(p => p.Score),
                    AvgAccuracy: g.Average(p =>
                        p.ActionHistory.Count == 0
                            ? 0
                            : (double)p.ActionHistory.Count(r => r.IsGoodMove) / p.ActionHistory.Count * 100),
                    TopMistakes: g
                        .SelectMany(p => p.ActionHistory.Where(r => !r.IsGoodMove))
                        .GroupBy(r => r.EventTitle)
                        .OrderByDescending(eg => eg.Count())
                        .Take(3)
                        .Select(eg => new TopMistakeDto(
                            EventTitle: eg.Key,
                            Count: eg.Count(),
                            EducationalNote: eg.First().EducationalNote))))
        );

        await Clients.Client(room!.HostConnectionId).SendAsync("GlobalDebrief", globalDebrief);

        logger.LogInformation("Room {RoomId} terminée — raison: {Reason}", roomId, reason);
    }

    private static PlayerDebriefDto BuildPlayerDebrief(IEnumerable<PlayerActionRecord> history)
    {
        var list = history.ToList();
        return new PlayerDebriefDto(
            TotalScore: list.Sum(r => r.ScoreApplied),
            TotalCarbon: list.Sum(r => r.CarbonApplied),
            GoodMoves: list.Count(r => r.IsGoodMove),
            BadMoves: list.Count(r => !r.IsGoodMove),
            Mistakes: list
                .Where(r => !r.IsGoodMove)
                .Select(r => new MistakeDto(
                    EventTitle: r.EventTitle,
                    ActionLabel: r.ActionLabel,
                    PrerequisitesMet: r.PrerequisitesMet,
                    EducationalNote: r.EducationalNote))
        );
    }
}