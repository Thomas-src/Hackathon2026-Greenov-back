using Backend.Services;
using Microsoft.AspNetCore.SignalR;

namespace Backend.Hubs;

public class QuizHub : Hub
{
    private readonly RoomService _roomService;

    public QuizHub(RoomService roomService)
    {
        _roomService = roomService;
    }

    // HOST crée une room
    public async Task CreateRoom(string roomId)
    {
        _roomService.SetHost(roomId, Context.ConnectionId);

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        await Clients.Caller.SendAsync("RoomCreated", roomId);
    }

    // PLAYER rejoint une room
    public async Task JoinRoom(string roomId, string username)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        await Clients.Group(roomId)
            .SendAsync("UserJoined", username);
    }

    // HOST lance le quiz
    public async Task StartQuiz(string roomId)
    {
        var host = _roomService.GetHost(roomId);

        if (host != Context.ConnectionId)
            return; // sécurité simple

        await Clients.Group(roomId)
            .SendAsync("QuizStarted");
    }

    // PLAYER répond
    public async Task SubmitAnswer(string roomId, string username, string answer)
    {
        await Clients.Group(roomId)
            .SendAsync("ReceiveAnswer", username, answer);
    }
}