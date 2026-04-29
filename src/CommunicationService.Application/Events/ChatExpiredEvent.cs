namespace CommunicationService.Application.Events;

public sealed record ChatExpiredEvent(
    Guid RoomId,
    DateTimeOffset ExpiredAt);
