namespace Backend.Dto.Event;

public record NewEventDto(
    string Id,
    string Title,
    string Description,
    int EventNumber,
    int TotalEvents,
    IEnumerable<ActionChoiceDto> Actions
);