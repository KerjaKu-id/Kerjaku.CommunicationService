using CommunicationService.Domain.Enums;
using CommunicationService.Tests.Integration.TestUtilities;

namespace CommunicationService.Tests.Integration.Gateway;

[Collection("GatewayE2E")]
public class GatewayMessagesTests
{
    private readonly GatewayApiClient _api;
    private readonly GatewayDbHelper _db;

    public GatewayMessagesTests(GatewayTestFixture fixture)
    {
        _api = new GatewayApiClient(fixture.Client);
        _db = new GatewayDbHelper(fixture);
    }

    [Fact]
    public async Task SendMessage_PersistsMessageAndStatuses()
    {
        var senderId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var room = await _api.CreateRoomAsync(senderId, recipientId);

        var message = await _api.SendMessageAsync(room.Id, senderId, "hello");

        var storedMessage = await _db.GetMessageAsync(message.Id);
        Assert.NotNull(storedMessage);
        Assert.Equal(room.Id, storedMessage!.ChatRoomId);
        Assert.Equal(senderId, storedMessage.SenderId);
        Assert.Equal("hello", storedMessage.Content);

        var statusCount = await _db.CountMessageStatusesAsync(message.Id);
        Assert.Equal(1, statusCount);

        var isSent = await _db.IsMessageStatusAsync(message.Id, recipientId, MessageDeliveryStatus.Sent);
        Assert.True(isSent);
    }

    [Fact]
    public async Task GetMessages_ReturnsPagedResults()
    {
        var senderId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var room = await _api.CreateRoomAsync(senderId, recipientId);

        await _api.SendMessageAsync(room.Id, senderId, "first");
        await _api.SendMessageAsync(room.Id, senderId, "second");

        var response = await _api.GetMessagesAsync(room.Id, pageNumber: 1, pageSize: 1);

        Assert.Single(response.Data.Items);
        Assert.True(response.Data.HasNext);

        var messageCount = await _db.CountMessagesAsync(room.Id);
        Assert.Equal(2, messageCount);
    }
}
