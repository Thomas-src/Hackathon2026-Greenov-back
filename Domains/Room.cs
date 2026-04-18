namespace Backend.Domains;

public class Room
{
    public string RoomId { get; init; } = "";
    public string HostConnectionId { get; set; } = "";
    public Dictionary<string, Player> Players { get; set; } = new();
    public GameEvent? ActiveEvent { get; set; }
    public int GlobalCarbon { get; set; } = 0;
}