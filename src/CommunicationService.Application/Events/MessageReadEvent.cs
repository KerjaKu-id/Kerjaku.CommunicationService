namespace CommunicationService.Application.Events;

public sealed record MessageReadEvent(
    Guid MessageId,
    Guid RoomId,
    Guid ReaderId,
    DateTimeOffset ReadAt);
