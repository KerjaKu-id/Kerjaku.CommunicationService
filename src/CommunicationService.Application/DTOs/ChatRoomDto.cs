namespace CommunicationService.Application.DTOs;

public sealed class ChatRoomDto
{
    public Guid Id { get; init; }
    public bool IsTemporary { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public bool IsExpired { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public IReadOnlyCollection<Guid> Participants { get; init; } = Array.Empty<Guid>();
}
