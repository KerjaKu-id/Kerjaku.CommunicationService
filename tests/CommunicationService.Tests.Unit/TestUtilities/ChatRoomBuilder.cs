using CommunicationService.Domain.Entities;

namespace CommunicationService.Tests.Unit.TestUtilities;

public sealed class ChatRoomBuilder
{
    private bool _isTemporary;
    private DateTimeOffset? _expiresAt;
    private DateTimeOffset _createdAt = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero);
    private readonly List<Guid> _participants = new();

    public ChatRoomBuilder WithTemporary(DateTimeOffset? expiresAt)
    {
        _isTemporary = true;
        _expiresAt = expiresAt;
        return this;
    }

    public ChatRoomBuilder WithCreatedAt(DateTimeOffset createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public ChatRoomBuilder AddParticipant(Guid userId)
    {
        _participants.Add(userId);
        return this;
    }

    public ChatRoom Build()
    {
        var room = new ChatRoom(_isTemporary, _expiresAt, _createdAt);
        foreach (var userId in _participants)
        {
            room.AddParticipant(userId, _createdAt);
        }

        return room;
    }
}
