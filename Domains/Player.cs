namespace Backend.Domains;

public class Player
{
    public string ConnectionId { get; init; } = "";
    public string Username { get; set; } = "";
    public string Profile { get; set; } = "";   // "proprietaire" | "locataire" | ...
    public int Score { get; set; } = 0;
    public int CarbonLevel { get; set; } = 100; // 100 = neutre, descend si bonne décision
    public List<PlayerActionRecord> ActionHistory { get; set; } = [];
}