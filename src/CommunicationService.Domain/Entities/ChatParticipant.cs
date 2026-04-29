namespace CommunicationService.Domain.Entities;

public class ChatParticipant
{
    private ChatParticipant()
    {
    }

    public ChatParticipant(Guid chatRoomId, Guid userId, DateTimeOffset joinedAt)
    {
        Id = Guid.NewGuid();
        ChatRoomId = chatRoomId;
        UserId = userId;
        JoinedAt = joinedAt;
    }

    public Guid Id { get; private set; }
    public Guid ChatRoomId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }

    public ChatRoom ChatRoom { get; private set; } = null!;
}
