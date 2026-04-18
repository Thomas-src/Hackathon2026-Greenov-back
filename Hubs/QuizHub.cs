using Backend.Catalog.Models;
using Backend.Domains;
using Backend.Services;
using Backend.Utils;
using Microsoft.AspNetCore.SignalR;

namespace Backend.Hubs;

/// <summary>
/// Hub SignalR principal gérant le déroulement d’une partie de quiz en temps réel.
///
/// ── Méthodes Host ─────────────────────────────────────────
/// CreateRoom(int durationMinutes)
/// → Crée une room avec une durée donnée et initialise les événements.
///
/// StartGame(string roomId)
/// → Démarre la partie, lance le timer global et envoie le premier event aux joueurs + les données à l'host.
///
/// GetGlobalDebrief(string roomId)
/// → Retourne un résumé global de la partie (scores, stats, historique) au host.
///
/// ForceEndGame(string roomId)
/// → Force la fin de la partie (interruption manuelle).
///
/// ── Méthodes Player ───────────────────────────────────────
/// JoinRoom(string roomId, string username, string profile)
/// → Permet à un joueur de rejoindre une room avec un profil donné.
///
/// SubmitAction(string roomId, string actionId)
/// → Soumet une action pour l’event courant et applique score/carbone.
///
/// GetMyDebrief(string roomId)
/// → Retourne le résumé individuel du joueur (score, erreurs, stats).
///
/// ── Cycle de vie ──────────────────────────────────────────
/// OnDisconnectedAsync(Exception? exception)
/// → Gère la déconnexion d’un joueur et le retire de la room.
///
/// ── Comportement interne ──────────────────────────────────
/// - Gestion des rooms, joueurs et état via RoomManager
/// - Évaluation des actions via ActionEngine
/// - Distribution des événements via EventCatalog
/// - Communication temps réel via SignalR (clients ciblés et groupes)
/// </summary>
public class QuizHub(
    RoomManager roomManager,
    ActionEngine actionEngine,
    EventCatalog eventCatalog,
    ILogger<QuizHub> logger)
    : Hub
{
    // ── HOST ───────────────────────────────────────────────

    public async Task<string> CreateRoom(int durationMinutes)
    {
        var roomId = RoomIdGenerator.Generate();
        roomManager.CreateRoom(Context.ConnectionId, roomId);

        var allEvents = eventCatalog.GetAll();
        roomManager.ConfigureRoom(roomId, durationMinutes, allEvents);

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        var room = roomManager.GetRoom(roomId)!;
        logger.LogInformation("Room {RoomId} créée — {Duration} min — {Total} events",
            roomId, durationMinutes, room.TotalEvents);

        return roomId;
    }

    public async Task StartGame(string roomId)
    {
        logger.LogInformation("StartGame called — roomId: {RoomId}, caller: {ConnId}, host: {HostConnId}, isHost: {IsHost}",
            roomId,
            Context.ConnectionId,
            roomManager.GetRoom(roomId)?.HostConnectionId ?? "null",
            roomManager.IsHost(roomId, Context.ConnectionId));

        if (!roomManager.IsHost(roomId, Context.ConnectionId)) return;

        var room = roomManager.GetRoom(roomId);
        if (room == null || room.IsStarted) return;

        roomManager.StartRoom(roomId);

        var time = new DateTimeOffset((room.StartedAt ?? DateTime.Now + TimeSpan.FromMinutes(room.DurationMinutes)).ToUniversalTime());

        // host
        await Clients.Caller.SendAsync("GameStartedHost", new
        {
            Remaining = time.ToUnixTimeSeconds(),
            Players = room.Players.Values,
            Carbon = room.GlobalCarbon
        });

        var cts = new CancellationTokenSource();
        roomManager.SetTimerCts(roomId, cts);
        _ = RunGameTimerAsync(roomId, room.DurationMinutes, cts.Token);

        // Premier event envoyé individuellement à chaque joueur
        foreach (var (connectionId, player) in room.Players)
        {
            await Clients.Client(connectionId).SendAsync("GameStartedPlayer", new
            {
                HouseState = player.Score,
                Carbon = player.CarbonLevel,
                Profile = player.Profile
            }, cancellationToken: cts.Token);

            await SendNextEventToPlayer(roomId, connectionId);
        }
    }

    public async Task GetGlobalDebrief(string roomId)
    {
        if (!roomManager.IsHost(roomId, Context.ConnectionId)) return;

        var players = roomManager.GetAllPlayers(roomId);

        var debrief = players.Select(p => new
        {
            p.Username,
            p.Profile,
            p.Score,
            p.CarbonLevel,
            Accuracy = p.ActionHistory.Count == 0
                ? 0
                : (double)p.ActionHistory.Count(r => r.IsGoodMove) / p.ActionHistory.Count * 100,
            History = p.ActionHistory,
        });

        await Clients.Caller.SendAsync("GlobalDebrief", debrief);
    }

    public async Task ForceEndGame(string roomId)
    {
        if (!roomManager.IsHost(roomId, Context.ConnectionId)) return;

        var room = roomManager.GetRoom(roomId);
        if (room == null || !room.IsStarted) return;

        await EndGame(roomId, "forced");
    }

    // ── PLAYER ─────────────────────────────────────────────

    public async Task JoinRoom(string roomId, string username, string profile)
    {
        var room = roomManager.GetRoom(roomId);

        if (room == null)
        {
            await Clients.Caller.SendAsync("JoinFailed", new
            {
                Message = "Session introuvable. Vérifie le code et réessaie."
            });
            return;
        }

        if (room.IsStarted)
        {
            await Clients.Caller.SendAsync("JoinFailed", new
            {
                Message = "La partie a déjà commencé."
            });
            return;
        }

        roomManager.AddPlayer(roomId, Context.ConnectionId, username, profile);
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        logger.LogInformation("Player {Username} ({Profile}) a rejoint {RoomId}", username, profile, roomId);

        await Clients.Caller.SendAsync("JoinSuccess", new
        {
            Username = username,
            Profile = profile,
        });

        await Clients.Group(roomId).SendAsync("PlayerJoined", new
        {
            Username = username,
            Profile = profile,
            PlayerCount = room.Players.Count,
        });
    }

    public async Task SubmitAction(string roomId, string actionId)
    {
        var room = roomManager.GetRoom(roomId);
        if (room == null || !room.IsStarted) return;
        if (!room.Players.TryGetValue(Context.ConnectionId, out var player)) return;

        var gameEvent = roomManager.GetCurrentEventForPlayer(roomId, Context.ConnectionId, player.Profile);
        var action = gameEvent?.Actions.FirstOrDefault(a => a.Id == actionId);
        if (action == null || gameEvent == null) return;

        var history = roomManager.GetPlayerActionHistory(roomId, Context.ConnectionId, gameEvent.Id);
        var result = actionEngine.Evaluate(action, history);

        roomManager.RecordAction(roomId, Context.ConnectionId, gameEvent, action, result);
        roomManager.ApplyScore(roomId, Context.ConnectionId, result.ScoreApplied);
        roomManager.ApplyCarbonGlobal(roomId, result.CarbonApplied);

        var newScore = roomManager.GetScore(roomId, Context.ConnectionId);

        await Clients.Client(room.HostConnectionId).SendAsync("VillageUpdate", new
        {
            player.Username,
            player.Profile,
            result.IsGoodMove,
            GlobalCarbon = roomManager.GetGlobalCarbon(roomId),
            PlayerScore = newScore,
        });

        await Clients.Caller.SendAsync("ActionFeedback", new
        {
            result.ActionId,
            result.IsGoodMove,
            result.PrerequisitesMet,
            NewScore = newScore,
            EducationalNote = gameEvent.EducationalNote,
            HasNextEvent = roomManager.HasMoreEventsForPlayer(roomId, Context.ConnectionId, player.Profile),
        });

        await SendNextEventToPlayer(roomId, Context.ConnectionId);
    }

    public async Task GetMyDebrief(string roomId)
    {
        var history = roomManager.GetPlayerHistory(roomId, Context.ConnectionId);

        var debrief = new
        {
            TotalScore = history.Sum(r => r.ScoreApplied),
            TotalCarbon = history.Sum(r => r.CarbonApplied),
            Mistakes = history
                .Where(r => !r.IsGoodMove)
                .Select(r => new
                {
                    r.EventTitle,
                    r.ActionLabel,
                    r.PrerequisitesMet,
                    r.EducationalNote,
                }),
            GoodMoves = history.Count(r => r.IsGoodMove),
            BadMoves = history.Count(r => !r.IsGoodMove),
        };

        await Clients.Caller.SendAsync("Debrief", debrief);
    }

    // ── Déconnexion ────────────────────────────────────────

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        roomManager.RemovePlayer(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // ── Privé ──────────────────────────────────────────────

    private async Task SendNextEventToPlayer(string roomId, string connectionId)
    {
        var room = roomManager.GetRoom(roomId);
        if (room == null) return;

        if (!room.Players.TryGetValue(connectionId, out var player)) return;

        var nextEvent = roomManager.GetNextEventForPlayer(roomId, connectionId, player.Profile);
        if (nextEvent == null)
        {
            // Ce joueur n'a plus d'events — il attend les autres
            await Clients.Client(connectionId).SendAsync("WaitingForOthers", new
            {
                Message = "Tu as terminé toutes tes tâches !",
                Score = roomManager.GetScore(roomId, connectionId),
            });

            if (roomManager.AllPlayersFinished(roomId))
                await EndGame(roomId, "completed");

            return;
        }

        room.PlayerEventIndex.TryGetValue(connectionId, out var index);

        await Clients.Client(connectionId).SendAsync("NewEvent", new
        {
            nextEvent.Id,
            nextEvent.Title,
            nextEvent.Description,
            EventNumber = index,
            TotalEvents = room.TotalEvents,
            Actions = nextEvent.Actions.Select(a => new { a.Id, a.Label })
        });
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
                    await Clients.Group(roomId).SendAsync("TimerTick", new
                    {
                        Remaining = remaining,
                        Total = totalSeconds,
                    });
                }
            }

            if (!ct.IsCancellationRequested)
                await EndGame(roomId, "timeout");
        }
        catch (TaskCanceledException)
        {
        }
    }

    private async Task EndGame(string roomId, string reason)
    {
        roomManager.CancelTimer(roomId);

        var players = roomManager.GetAllPlayers(roomId);

        // Debrief individuel → chaque joueur reçoit son propre résumé
        foreach (var player in players)
        {
            var history = roomManager.GetPlayerHistory(roomId, player.ConnectionId);

            await Clients.Client(player.ConnectionId).SendAsync("Debrief", new
            {
                TotalScore = history.Sum(r => r.ScoreApplied),
                TotalCarbon = history.Sum(r => r.CarbonApplied),
                GoodMoves = history.Count(r => r.IsGoodMove),
                BadMoves = history.Count(r => !r.IsGoodMove),
                Mistakes = history
                    .Where(r => !r.IsGoodMove)
                    .Select(r => new
                    {
                        r.EventTitle,
                        r.ActionLabel,
                        r.PrerequisitesMet,
                        r.EducationalNote,
                    }),
            });
        }

        // Recap global → host uniquement
        var room = roomManager.GetRoom(roomId);
        var statsByProfile = players
            .GroupBy(p => p.Profile)
            .Select(g => new
            {
                Profile = g.Key,
                PlayerCount = g.Count(),
                AvgScore = g.Average(p => p.Score),
                AvgAccuracy = g.Average(p =>
                    p.ActionHistory.Count == 0
                        ? 0
                        : (double)p.ActionHistory.Count(r => r.IsGoodMove) / p.ActionHistory.Count * 100),
                // Erreurs les plus fréquentes pour ce profil
                TopMistakes = g
                    .SelectMany(p => p.ActionHistory.Where(r => !r.IsGoodMove))
                    .GroupBy(r => r.EventTitle)
                    .OrderByDescending(eg => eg.Count())
                    .Take(3)
                    .Select(eg => new
                    {
                        EventTitle = eg.Key,
                        Count = eg.Count(),
                        EducationalNote = eg.First().EducationalNote,
                    }),
            });

        await Clients.Client(room!.HostConnectionId).SendAsync("GlobalDebrief", new
        {
            Reason = reason,
            GlobalCarbon = roomManager.GetGlobalCarbon(roomId),
            Leaderboard = players
                .OrderByDescending(p => p.Score)
                .Select((p, i) => new
                {
                    Rank = i + 1,
                    p.Username,
                    p.Profile,
                    p.Score,
                }),
            StatsByProfile = statsByProfile,
        });

        logger.LogInformation("Room {RoomId} terminée — raison: {Reason}", roomId, reason);
    }
}