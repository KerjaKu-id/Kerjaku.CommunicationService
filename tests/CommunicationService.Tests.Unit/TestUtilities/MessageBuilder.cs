using CommunicationService.Domain.Entities;
using CommunicationService.Domain.Enums;

namespace CommunicationService.Tests.Unit.TestUtilities;

public sealed class MessageBuilder
{
    private Guid _roomId = Guid.NewGuid();
    private Guid _senderId = Guid.NewGuid();
    private MessageType _type = MessageType.Text;
    private string _content = "hello";
    private DateTimeOffset _createdAt = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero);
    private readonly List<(Guid RecipientId, MessageDeliveryStatus Status, DateTimeOffset UpdatedAt)> _statuses = new();

    public MessageBuilder WithRoomId(Guid roomId)
    {
        _roomId = roomId;
        return this;
    }

    public MessageBuilder WithSender(Guid senderId)
    {
        _senderId = senderId;
        return this;
    }

    public MessageBuilder WithType(MessageType type)
    {
        _type = type;
        return this;
    }

    public MessageBuilder WithContent(string content)
    {
        _content = content;
        return this;
    }

    public MessageBuilder WithCreatedAt(DateTimeOffset createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public MessageBuilder AddStatus(Guid recipientId, MessageDeliveryStatus status, DateTimeOffset updatedAt)
    {
        _statuses.Add((recipientId, status, updatedAt));
        return this;
    }

    public Message Build()
    {
        var message = new Message(_roomId, _senderId, _type, _content, _createdAt);
        foreach (var (recipientId, status, updatedAt) in _statuses)
        {
            message.AddStatus(recipientId, status, updatedAt);
        }

        return message;
    }
}
