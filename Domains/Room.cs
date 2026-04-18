namespace Backend.Domains;

public class Room
{
    public string RoomId { get; init; } = "";
    public string HostConnectionId { get; set; } = "";
    public Dictionary<string, Player> Players { get; set; } = new();
    public Dictionary<string, int> PlayerEventIndex { get; set; } = new();
    public int GlobalCarbon { get; set; } = 0;

    public int DurationMinutes { get; set; }
    public int TotalEvents { get; set; }
    public List<GameEvent> EventQueue { get; set; } = [];
    public bool IsStarted { get; set; } = false;
    public DateTime? StartedAt { get; set; }
    public CancellationTokenSource? TimerCts { get; set; }
}