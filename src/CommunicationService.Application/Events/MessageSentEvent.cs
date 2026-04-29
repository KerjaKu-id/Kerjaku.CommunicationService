using CommunicationService.Domain.Enums;

namespace CommunicationService.Application.Events;

public sealed record MessageSentEvent(
    Guid MessageId,
    Guid RoomId,
    Guid SenderId,
    MessageType Type,
    DateTimeOffset CreatedAt);
