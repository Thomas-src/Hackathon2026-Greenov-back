using System.Text.Json;
using Backend.Domains;

namespace Backend.Catalog.Models;

public class EventCatalog
{
    private readonly List<GameEvent> _events;

    public EventCatalog(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "Catalog", "events.json");
        var json = File.ReadAllText(path);

        var entries = JsonSerializer.Deserialize<List<EventCatalogEntry>>(json)
                      ?? throw new InvalidOperationException("events.json invalide ou vide");

        _events = entries.Select(MapToDomain).ToList();
    }

    public GameEvent? Get(string id) =>
        _events.FirstOrDefault(e => e.Id == id);

    public List<GameEvent> GetAll() => _events;

    public List<GameEvent> GetForProfile(string profile) =>
        _events.Where(e => string.IsNullOrEmpty(e.ProfileFilter)
                           || e.ProfileFilter == profile).ToList();

    private static GameEvent MapToDomain(EventCatalogEntry e) => new()
    {
        Id = e.Id,
        Title = e.Title,
        Type = e.Type,
        Description = e.Description,
        ProfileFilter = e.ProfileFilter,
        EducationalNote = e.EducationalNote,
        Actions = e.Actions.Select(a => new GameAction
        {
            Id = a.Id,
            Label = a.Label,
            RequiredBefore = a.RequiredBefore,
            ScoreDelta = a.ScoreDelta,
            CarbonDelta = a.CarbonDelta,
            IsGoodAnswer = a.IsGoodAnswer
        }).ToList()
    };
}