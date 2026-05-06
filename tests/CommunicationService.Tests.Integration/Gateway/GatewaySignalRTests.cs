using CommunicationService.Application.DTOs;
using CommunicationService.Application.DTOs.Requests;
using CommunicationService.Domain.Enums;
using CommunicationService.Tests.Integration.TestUtilities;
using Microsoft.AspNetCore.SignalR.Client;
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

        await using var senderConnection = CreateHubConnection();
        await using var receiverConnection = CreateHubConnection();

        var receivedMessage = new TaskCompletionSource<MessageDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        receiverConnection.On<MessageDto>("ReceiveMessage", message => receivedMessage.TrySetResult(message));

        await senderConnection.StartAsync();
        await receiverConnection.StartAsync();

        await senderConnection.InvokeAsync("JoinRoom", room.Id, user1);
        await receiverConnection.InvokeAsync("JoinRoom", room.Id, user2);

        var request = new SendMessageRequest
        {
            RoomId = room.Id,
            SenderId = user1,
            Type = MessageType.Text,
            Content = "hello"
        };

        await senderConnection.InvokeAsync("SendMessage", request);

        var message = await receivedMessage.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(room.Id, message.RoomId);
        Assert.Equal(user1, message.SenderId);
    }

    [Fact]
    public async Task MarkRead_UpdatesStatusInDatabase()
    {
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var room = await _api.CreateRoomAsync(user1, user2);

        await using var senderConnection = CreateHubConnection();
        await using var receiverConnection = CreateHubConnection();

        var receivedMessage = new TaskCompletionSource<MessageDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        receiverConnection.On<MessageDto>("ReceiveMessage", message => receivedMessage.TrySetResult(message));

        var readStatus = new TaskCompletionSource<MessageStatusDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        senderConnection.On<MessageStatusDto>("MessageRead", status => readStatus.TrySetResult(status));

        await senderConnection.StartAsync();
        await receiverConnection.StartAsync();

        await senderConnection.InvokeAsync("JoinRoom", room.Id, user1);
        await receiverConnection.InvokeAsync("JoinRoom", room.Id, user2);

        var request = new SendMessageRequest
        {
            RoomId = room.Id,
            SenderId = user1,
            Type = MessageType.Text,
            Content = "hello"
        };

        await senderConnection.InvokeAsync("SendMessage", request);
        var message = await receivedMessage.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await receiverConnection.InvokeAsync("MarkRead", message.Id, user2);
        var status = await readStatus.Task.WaitAsync(TimeSpan.FromSeconds(10));

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

    private HubConnection CreateHubConnection()
    {
        var hubUri = new Uri(_fixture.ChatBaseUri, "hubs/chat");

        return new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.Transports = HttpTransportType.WebSockets;
            })
            .Build();
    }
}
