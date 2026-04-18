namespace Backend.Domains;

public class GameEvent
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Type { get; init; } = "";
    public string Description { get; init; } = "";
    public string ProfileFilter { get; init; } = "";
    public string EducationalNote { get; init; } = "";
    public List<GameAction> Actions { get; init; } = [];
}

public class GameAction
{
    public string Id { get; init; } = "";             // "A", "B", "C"
    public string Label { get; init; } = "";          // "Appeler un technicien"
    public List<string> RequiredBefore { get; init; } = [];  // ["A"] = B nécessite A d'abord
    public int ScoreDelta { get; init; }              // +10 ou -15
    public int CarbonDelta { get; init; }             // -5 ou +20
    public required bool IsGoodAnswer { get; init; }
}