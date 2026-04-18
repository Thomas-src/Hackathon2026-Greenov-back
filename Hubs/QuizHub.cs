using Backend.Catalog.Models;
using Backend.Domains;
using Backend.Services;
using Backend.Utils;
using Microsoft.AspNetCore.SignalR;

namespace Backend.Hubs;

public class QuizHub : Hub
{
    private readonly ActionEngine _actionEngine;
    private readonly EventCatalog _eventCatalog;
    private readonly RoomManager _roomManager;

    public QuizHub(RoomManager roomManager, ActionEngine actionEngine, EventCatalog eventCatalog)
    {
        _roomManager = roomManager;
        _actionEngine = actionEngine;
        _eventCatalog = eventCatalog;
    }

    // HOST crée room
    public async Task<string> CreateRoom()
    {
        var roomId = RoomIdGenerator.Generate();

        _roomManager.CreateRoom(Context.ConnectionId, roomId);

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        return roomId;
    }

    // PLAYER join
    public async Task JoinRoom(string roomId, string username)
    {
        var room = _roomManager.GetRoom(roomId);
        if (room == null) return;

        _roomManager.AddPlayer(roomId, Context.ConnectionId, username);

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        // notify room
        await Clients.Group(roomId)
            .SendAsync("PlayerJoined", username);
    }

    // HOST broadcast question
    public async Task SendQuestion(string roomId, string question)
    {
        if (!_roomManager.IsHost(roomId, Context.ConnectionId))
            return;

        await Clients.Group(roomId)
            .SendAsync("NewQuestion", question);
    }

    // PLAYER answer
    public async Task SubmitAnswer(string roomId, string answer)
    {
        var room = _roomManager.GetRoom(roomId);
        if (room == null) return;

        if (!room.Players.TryGetValue(Context.ConnectionId, out var player))
            return;

        // broadcast answer to host only
        await Clients.Client(room.HostConnectionId)
            .SendAsync("ReceiveAnswer", player.Username, answer);
    }

    // HOST envoie un événement aux joueurs
    public async Task SendEvent(string roomId, string eventId)
    {
        if (!_roomManager.IsHost(roomId, Context.ConnectionId)) return;

        var gameEvent = _eventCatalog.Get(eventId);
        if (gameEvent == null) return;

        _roomManager.SetActiveEvent(roomId, gameEvent);

        await Clients.Group(roomId).SendAsync("NewEvent", new
        {
            gameEvent.Id,
            gameEvent.Title,
            gameEvent.Description,
            Actions = gameEvent.Actions.Select(a => new { a.Id, a.Label })
            // Ne pas envoyer RequiredBefore au client !
        });
    }

    // Dans QuizHub.cs

    public async Task SubmitAction(string roomId, string actionId)
    {
        var room = _roomManager.GetRoom(roomId);
        if (room == null) return;
        if (!room.Players.TryGetValue(Context.ConnectionId, out var player)) return;

        var gameEvent = _roomManager.GetActiveEvent(roomId);
        var action = gameEvent?.Actions.FirstOrDefault(a => a.Id == actionId);
        if (action == null || gameEvent == null) return;

        var history = _roomManager.GetPlayerActionHistory(roomId, Context.ConnectionId, gameEvent.Id);
        var result = _actionEngine.Evaluate(action, history, gameEvent);

        // Persistance du parcours joueur
        _roomManager.RecordAction(roomId, Context.ConnectionId, gameEvent, action, result);
        _roomManager.ApplyScore(roomId, Context.ConnectionId, result.ScoreApplied);
        _roomManager.ApplyCarbonGlobal(roomId, result.CarbonApplied);

        await Clients.Caller.SendAsync("ActionFeedback", new
        {
            result.ActionId,
            result.IsGoodMove,
            result.PrerequisitesMet,
            NewScore = _roomManager.GetScore(roomId, Context.ConnectionId),
        });

        await Clients.Group(roomId).SendAsync("VillageUpdate", new
        {
            player.Username,
            result.IsGoodMove,
            GlobalCarbon = _roomManager.GetGlobalCarbon(roomId),
            PlayerScore = _roomManager.GetScore(roomId, Context.ConnectionId),
        });
    }

// Appelé par le client en fin de partie pour son bilan individuel
    public async Task GetMyDebrief(string roomId)
    {
        var history = _roomManager.GetPlayerHistory(roomId, Context.ConnectionId);

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

    // Appelé par l'host pour le bilan global (graphiques par profil)
    public async Task GetGlobalDebrief(string roomId)
    {
        if (!_roomManager.IsHost(roomId, Context.ConnectionId)) return;

        var players = _roomManager.GetAllPlayers(roomId);

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

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _roomManager.RemovePlayer(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}