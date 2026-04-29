using CommunicationService.Domain.Enums;

namespace CommunicationService.Application.DTOs.Requests;

public sealed class SendMessageRequest
{
    public Guid RoomId { get; init; }
    public Guid SenderId { get; init; }
    public MessageType Type { get; init; }
    public string Content { get; init; } = string.Empty;
}
