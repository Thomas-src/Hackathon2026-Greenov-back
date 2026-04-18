namespace Backend.Domains;

public class Room
{
    public string RoomId { get; set; } = "";
    public string HostConnectionId { get; set; } = "";

    public Dictionary<string, Player> Players { get; set; } = new();
}