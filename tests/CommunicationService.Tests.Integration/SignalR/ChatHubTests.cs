using System.Net.Http.Json;
using CommunicationService.Api.Models;
using CommunicationService.Application.DTOs;
using CommunicationService.Application.DTOs.Requests;
using CommunicationService.Domain.Enums;
using CommunicationService.Tests.Integration.TestUtilities;
using Microsoft.AspNetCore.SignalR.Client;

namespace CommunicationService.Tests.Integration.SignalR;

public class ChatHubTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ChatHubTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SendMessage_BroadcastsToRoom()
    {
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var room = await CreateRoomAsync(user1, user2);

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

        var message = await receivedMessage.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(room.Id, message.RoomId);
        Assert.Equal(user1, message.SenderId);
    }

    [Fact]
    public async Task MarkRead_BroadcastsStatus()
    {
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var room = await CreateRoomAsync(user1, user2);

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
        var message = await receivedMessage.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await receiverConnection.InvokeAsync("MarkRead", message.Id, user2);
        var status = await readStatus.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(message.Id, status.MessageId);
        Assert.Equal(user2, status.RecipientId);
    }

    private async Task<ChatRoomDto> CreateRoomAsync(Guid user1, Guid user2)
    {
        var client = _factory.CreateClient();
        var request = new CreateChatRoomRequest
        {
            ParticipantIds = new[] { user1, user2 },
            IsTemporary = false
        };

        var response = await client.PostAsJsonAsync("/chat/rooms", request);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<ChatRoomDto>>();
        return payload!.Data;
    }

    private HubConnection CreateHubConnection()
    {
        var baseAddress = _factory.Server.BaseAddress ?? new Uri("http://localhost");
        var hubUri = new Uri(baseAddress, "/hubs/chat");

        return new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();
    }
}
