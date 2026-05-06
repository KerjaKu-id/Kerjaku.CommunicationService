using CommunicationService.Domain.Entities;

namespace CommunicationService.Tests.Unit.Domain;

public class ChatRoomTests
{
    [Fact]
    public void AddParticipant_IgnoresDuplicates()
    {
        var userId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero);
        var room = new ChatRoom(false, null, now);

        room.AddParticipant(userId, now);
        room.AddParticipant(userId, now.AddMinutes(1));

        Assert.Single(room.Participants);
        Assert.Equal(userId, room.Participants.Single().UserId);
    }

    [Fact]
    public void HasExpired_ReturnsTrue_WhenMarkedExpired()
    {
        var now = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero);
        var room = new ChatRoom(false, null, now);

        room.MarkExpired();

        Assert.True(room.HasExpired(now));
    }

    [Fact]
    public void HasExpired_ReturnsTrue_WhenExpiresAtIsInPast()
    {
        var now = new DateTimeOffset(2026, 4, 30, 12, 0, 0, TimeSpan.Zero);
        var room = new ChatRoom(true, now.AddMinutes(-1), now.AddHours(-1));

        Assert.True(room.HasExpired(now));
    }

    [Fact]
    public void HasExpired_ReturnsFalse_WhenNotExpired()
    {
        var now = new DateTimeOffset(2026, 4, 30, 12, 0, 0, TimeSpan.Zero);
        var room = new ChatRoom(true, now.AddHours(1), now.AddHours(-1));

        Assert.False(room.HasExpired(now));
    }
}
