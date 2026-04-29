namespace CommunicationService.Application.DTOs.Requests;

public sealed class CreateChatRoomRequest
{
    public IReadOnlyCollection<Guid> ParticipantIds { get; init; } = Array.Empty<Guid>();
    public bool IsTemporary { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public int? TimeToLiveHours { get; init; }
}
