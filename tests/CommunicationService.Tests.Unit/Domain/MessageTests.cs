using CommunicationService.Domain.Entities;
using CommunicationService.Domain.Enums;

namespace CommunicationService.Tests.Unit.Domain;

public class MessageTests
{
    [Fact]
    public void AddStatus_AppendsStatus()
    {
        var now = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero);
        var message = new Message(Guid.NewGuid(), Guid.NewGuid(), MessageType.Text, "hello", now);
        var recipientId = Guid.NewGuid();

        message.AddStatus(recipientId, MessageDeliveryStatus.Sent, now.AddMinutes(1));

        var status = Assert.Single(message.Statuses);
        Assert.Equal(recipientId, status.RecipientId);
        Assert.Equal(MessageDeliveryStatus.Sent, status.Status);
    }
}
