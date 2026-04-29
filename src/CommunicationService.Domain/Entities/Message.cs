using CommunicationService.Domain.Enums;

namespace CommunicationService.Domain.Entities;

public class Message
{
    private readonly List<MessageStatus> _statuses = new();

    private Message()
    {
    }

    public Message(Guid chatRoomId, Guid senderId, MessageType type, string content, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        ChatRoomId = chatRoomId;
        SenderId = senderId;
        Type = type;
        Content = content;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid ChatRoomId { get; private set; }
    public Guid SenderId { get; private set; }
    public MessageType Type { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public ChatRoom ChatRoom { get; private set; } = null!;
    public IReadOnlyCollection<MessageStatus> Statuses => _statuses;

    public void AddStatus(Guid recipientId, MessageDeliveryStatus status, DateTimeOffset updatedAt)
    {
        _statuses.Add(new MessageStatus(Id, recipientId, status, updatedAt));
    }
}
