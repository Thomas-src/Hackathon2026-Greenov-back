using System.Text.Json.Serialization;

namespace Backend.Catalog.Models;

public class EventCatalogEntry
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("title")]
    public string Title { get; init; } = "";

    [JsonPropertyName("description")]
    public string Description { get; init; } = "";

    [JsonPropertyName("profileFilter")]
    public string ProfileFilter { get; init; } = ""; // "" = tous

    [JsonPropertyName("educationalNote")]
    public string EducationalNote { get; init; } = ""; // affiché dans le bilan

    [JsonPropertyName("actions")]
    public List<ActionCatalogEntry> Actions { get; init; } = [];
}

public class ActionCatalogEntry
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("label")]
    public string Label { get; init; } = "";

    [JsonPropertyName("requiredBefore")]
    public List<string> RequiredBefore { get; init; } = [];

    [JsonPropertyName("scoreDelta")]
    public int ScoreDelta { get; init; }

    [JsonPropertyName("carbonDelta")]
    public int CarbonDelta { get; init; }
}