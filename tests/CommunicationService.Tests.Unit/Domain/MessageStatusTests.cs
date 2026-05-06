using CommunicationService.Domain.Entities;
using CommunicationService.Domain.Enums;

namespace CommunicationService.Tests.Unit.Domain;

public class MessageStatusTests
{
    [Fact]
    public void UpdateStatus_DoesNothing_WhenStatusIsUnchanged()
    {
        var now = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero);
        var status = new MessageStatus(Guid.NewGuid(), Guid.NewGuid(), MessageDeliveryStatus.Sent, now);

        status.UpdateStatus(MessageDeliveryStatus.Sent, now.AddMinutes(5));

        Assert.Equal(MessageDeliveryStatus.Sent, status.Status);
        Assert.Equal(now, status.UpdatedAt);
    }

    [Fact]
    public void UpdateStatus_Updates_WhenStatusChanges()
    {
        var now = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero);
        var updated = now.AddMinutes(5);
        var status = new MessageStatus(Guid.NewGuid(), Guid.NewGuid(), MessageDeliveryStatus.Sent, now);

        status.UpdateStatus(MessageDeliveryStatus.Read, updated);

        Assert.Equal(MessageDeliveryStatus.Read, status.Status);
        Assert.Equal(updated, status.UpdatedAt);
    }
}
