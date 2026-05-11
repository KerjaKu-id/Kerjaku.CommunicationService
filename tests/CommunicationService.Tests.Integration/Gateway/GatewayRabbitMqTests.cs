using CommunicationService.Application.Events;
using CommunicationService.Tests.Integration.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace CommunicationService.Tests.Integration.Gateway;

[Collection("GatewayE2E")]
public class GatewayRabbitMqTests
{
    private readonly GatewayTestFixture _fixture;
    private readonly GatewayApiClient _api;

    public GatewayRabbitMqTests(GatewayTestFixture fixture)
    {
        _fixture = fixture;
        _api = new GatewayApiClient(fixture.Client);
    }

    [Fact]
    public async Task SendMessage_PublishesMessageSentEvent()
    {
        await _fixture.WaitForRabbitMqAsync();

        var senderId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var room = await _api.CreateRoomAsync(senderId, recipientId);

        await using var listener = new GatewayRabbitMqListener(_fixture.RabbitMq, "MessageSentEvent");
        var eventTask = listener.WaitForEventAsync<MessageSentEvent>(
            TimeSpan.FromSeconds(10),
            data => data.RoomId == room.Id && data.SenderId == senderId && data.Content == "hello");

        var message = await _api.SendMessageAsync(room.Id, senderId, "hello");
        var published = await eventTask;

        Assert.Equal(message.Id, published.MessageId);
        Assert.Equal(room.Id, published.RoomId);
        Assert.Equal(senderId, published.SenderId);
        Assert.Equal(message.Type, published.Type);
        Assert.Equal("hello", published.Content);
        Assert.Contains(recipientId, published.RecipientIds);
    }

    [Fact]
    public async Task MarkRead_PublishesMessageReadEvent()
    {
        await _fixture.WaitForRabbitMqAsync();

        var senderId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var room = await _api.CreateRoomAsync(senderId, recipientId);
        var message = await _api.SendMessageAsync(room.Id, senderId, "hello");

        await using var listener = new GatewayRabbitMqListener(_fixture.RabbitMq, "MessageReadEvent");
        var eventTask = listener.WaitForEventAsync<MessageReadEvent>(
            TimeSpan.FromSeconds(10),
            data => data.MessageId == message.Id && data.ReaderId == recipientId);

        await using var sender = new GatewaySignalRClient(_fixture.ChatBaseUri);
        await using var receiver = new GatewaySignalRClient(_fixture.ChatBaseUri);

        await sender.ConnectAsync();
        await receiver.ConnectAsync();

        await sender.JoinRoomAsync(room.Id, senderId);
        await receiver.JoinRoomAsync(room.Id, recipientId);

        await receiver.MarkReadAsync(message.Id, recipientId);
        var published = await eventTask;

        Assert.Equal(message.Id, published.MessageId);
        Assert.Equal(room.Id, published.RoomId);
        Assert.Equal(recipientId, published.ReaderId);
    }

    [Fact]
    public async Task TemporaryChat_Expiration_PublishesChatExpiredEvent()
    {
        await _fixture.WaitForRabbitMqAsync();

        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(5);

        await using var listener = new GatewayRabbitMqListener(_fixture.RabbitMq, "ChatExpiredEvent");
        var timeout = _fixture.ExpirationWaitTimeout;
        if (timeout < TimeSpan.FromSeconds(120))
        {
            timeout = TimeSpan.FromSeconds(120);
        }

        var room = await _api.CreateRoomAsync(user1, user2, isTemporary: true, expiresAt: expiresAt);
        var eventTask = listener.WaitForEventAsync<ChatExpiredEvent>(
            timeout,
            data => data.RoomId == room.Id);

        var published = await eventTask;

        Assert.Equal(room.Id, published.RoomId);

        var expired = await _fixture.WaitForConditionAsync(
            db => db.ChatRooms
                .AsNoTracking()
                .AnyAsync(r => r.Id == room.Id && r.IsExpired),
            _fixture.ExpirationWaitTimeout);

        Assert.True(expired);
    }
}
