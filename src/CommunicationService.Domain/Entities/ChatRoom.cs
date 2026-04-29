namespace CommunicationService.Domain.Entities;

public class ChatRoom
{
    private readonly List<ChatParticipant> _participants = new();
    private readonly List<Message> _messages = new();

    private ChatRoom()
    {
    }

    public ChatRoom(bool isTemporary, DateTimeOffset? expiresAt, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        IsTemporary = isTemporary;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
        IsExpired = false;
    }

    public Guid Id { get; private set; }
    public bool IsTemporary { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public bool IsExpired { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<ChatParticipant> Participants => _participants;
    public IReadOnlyCollection<Message> Messages => _messages;

    public void AddParticipant(Guid userId, DateTimeOffset joinedAt)
    {
        if (_participants.Any(p => p.UserId == userId))
        {
            return;
        }

        _participants.Add(new ChatParticipant(Id, userId, joinedAt));
    }

    public void MarkExpired()
    {
        IsExpired = true;
    }

    public bool HasExpired(DateTimeOffset now)
    {
        return IsExpired || (ExpiresAt.HasValue && ExpiresAt.Value <= now);
    }
}
