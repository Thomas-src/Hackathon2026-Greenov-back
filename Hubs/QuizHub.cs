using Backend.Domains;
using Backend.Utils;
using Microsoft.AspNetCore.SignalR;

namespace Backend.Hubs;

public class QuizHub : Hub
{
    private readonly RoomManager _roomManager;

    public QuizHub(RoomManager roomManager)
    {
        _roomManager = roomManager;
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

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _roomManager.RemovePlayer(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}