using CommunicationService.Application.DTOs.Requests;
using CommunicationService.Domain.Enums;
using CommunicationService.Tests.Integration.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace CommunicationService.Tests.Integration.Gateway;

[Collection("GatewayE2E")]
public class GatewaySignalRTests
{
    private readonly GatewayApiClient _api;
    private readonly GatewayDbHelper _db;
    private readonly GatewayTestFixture _fixture;

    public GatewaySignalRTests(GatewayTestFixture fixture)
    {
        _fixture = fixture;
        _api = new GatewayApiClient(fixture.Client);
        _db = new GatewayDbHelper(fixture);
    }

    [Fact]
    public async Task SendMessage_BroadcastsToRoom()
    {
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var room = await _api.CreateRoomAsync(user1, user2);

        await using var sender = new GatewaySignalRClient(_fixture.ChatBaseUri);
        await using var receiver = new GatewaySignalRClient(_fixture.ChatBaseUri);

        await sender.ConnectAsync();
        await receiver.ConnectAsync();

        await sender.JoinRoomAsync(room.Id, user1);
        await receiver.JoinRoomAsync(room.Id, user2);

        var receivedMessage = receiver.ExpectReceiveMessageAsync(TimeSpan.FromSeconds(10));

        var request = new SendMessageRequest
        {
            RoomId = room.Id,
            SenderId = user1,
            Type = MessageType.Text,
            Content = "hello"
        };

        await sender.SendMessageAsync(request);

        var message = await receivedMessage;
        Assert.Equal(room.Id, message.RoomId);
        Assert.Equal(user1, message.SenderId);
    }

    [Fact]
    public async Task MarkRead_UpdatesStatusInDatabase()
    {
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var room = await _api.CreateRoomAsync(user1, user2);

        await using var sender = new GatewaySignalRClient(_fixture.ChatBaseUri);
        await using var receiver = new GatewaySignalRClient(_fixture.ChatBaseUri);

        await sender.ConnectAsync();
        await receiver.ConnectAsync();

        await sender.JoinRoomAsync(room.Id, user1);
        await receiver.JoinRoomAsync(room.Id, user2);

        var receivedMessage = receiver.ExpectReceiveMessageAsync(TimeSpan.FromSeconds(10));

        var request = new SendMessageRequest
        {
            RoomId = room.Id,
            SenderId = user1,
            Type = MessageType.Text,
            Content = "hello"
        };

        await sender.SendMessageAsync(request);
        var message = await receivedMessage;

        var readStatus = sender.ExpectMessageReadAsync(TimeSpan.FromSeconds(10));
        await receiver.MarkReadAsync(message.Id, user2);
        var status = await readStatus;

        Assert.Equal(message.Id, status.MessageId);
        Assert.Equal(user2, status.RecipientId);

        var updated = await _fixture.WaitForConditionAsync(
            db => db.MessageStatuses
                .AsNoTracking()
                .AnyAsync(ms => ms.MessageId == message.Id
                                && ms.RecipientId == user2
                                && ms.Status == MessageDeliveryStatus.Read),
            TimeSpan.FromSeconds(10));

        Assert.True(updated);
    }
}
