using CommunicationService.Domain.Enums;

namespace CommunicationService.Application.DTOs;

public sealed class MessageStatusDto
{
    public Guid MessageId { get; init; }
    public Guid RoomId { get; init; }
    public Guid RecipientId { get; init; }
    public MessageDeliveryStatus Status { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
