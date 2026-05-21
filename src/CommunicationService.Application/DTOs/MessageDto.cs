using CommunicationService.Domain.Enums;

namespace CommunicationService.Application.DTOs;

public sealed class MessageDto
{
    public Guid Id { get; init; }
    public Guid RoomId { get; init; }
    public Guid SenderId { get; init; }
    public MessageType Type { get; init; }
    public string MessageType { get; init; } = "text";
    public string Content { get; init; } = string.Empty;
    public object? Metadata { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public IReadOnlyCollection<MessageStatusDto> Statuses { get; init; } = Array.Empty<MessageStatusDto>();
    public string? SenderName { get; init; }
    public string? SenderAvatar { get; init; }
}
