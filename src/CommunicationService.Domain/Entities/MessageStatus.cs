using CommunicationService.Domain.Enums;

namespace CommunicationService.Domain.Entities;

public class MessageStatus
{
    private MessageStatus()
    {
    }

    public MessageStatus(Guid messageId, Guid recipientId, MessageDeliveryStatus status, DateTimeOffset updatedAt)
    {
        Id = Guid.NewGuid();
        MessageId = messageId;
        RecipientId = recipientId;
        Status = status;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; private set; }
    public Guid MessageId { get; private set; }
    public Guid RecipientId { get; private set; }
    public MessageDeliveryStatus Status { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public Message Message { get; private set; } = null!;

    public void UpdateStatus(MessageDeliveryStatus status, DateTimeOffset updatedAt)
    {
        if (Status == status)
        {
            return;
        }

        Status = status;
        UpdatedAt = updatedAt;
    }
}
