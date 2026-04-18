namespace Backend.Domains;

public record PlayerActionSubmission(
    string RoomId,
    string ConnectionId,
    string EventId,
    string ActionId,
    DateTime SubmittedAt
);