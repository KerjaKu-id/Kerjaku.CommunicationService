using System.Net;
using System.Net.Http.Json;
using CommunicationService.Api.Models;
using CommunicationService.Application.DTOs;
using CommunicationService.Application.DTOs.Requests;
using CommunicationService.Domain.Enums;
using CommunicationService.Tests.Integration.TestUtilities;

namespace CommunicationService.Tests.Integration.Api;

public class MessagesEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MessagesEndpointTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SendMessage_ReturnsMessageAndLinks()
    {
        var senderId = Guid.NewGuid();
        var room = await CreateRoomAsync(senderId, Guid.NewGuid());

        var request = new SendMessageRequest
        {
            RoomId = room.Id,
            SenderId = senderId,
            Type = MessageType.Text,
            Content = "hello"
        };

        var response = await _client.PostAsJsonAsync("/chat/messages", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<MessageDto>>();
        Assert.NotNull(payload);
        Assert.Equal(room.Id, payload!.Data.RoomId);
        Assert.Contains(payload.Links, link => link.Rel == "self");
    }

    [Fact]
    public async Task GetMessages_ReturnsPagedResult_WithNextLink()
    {
        var senderId = Guid.NewGuid();
        var room = await CreateRoomAsync(senderId, Guid.NewGuid());

        await SendMessageAsync(room.Id, senderId, "first");
        await SendMessageAsync(room.Id, senderId, "second");

        var response = await _client.GetAsync($"/chat/messages?roomId={room.Id}&pageNumber=1&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<MessageDto>>>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.Data.Items.Count);
        Assert.True(payload.Data.HasNext);
        Assert.Contains(payload.Links, link => link.Rel == "next");
    }

    private async Task<ChatRoomDto> CreateRoomAsync(Guid user1, Guid user2)
    {
        var request = new CreateChatRoomRequest
        {
            ParticipantIds = new[] { user1, user2 },
            IsTemporary = false
        };

        var response = await _client.PostAsJsonAsync("/chat/rooms", request);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<ChatRoomDto>>();
        return payload!.Data;
    }

    private async Task SendMessageAsync(Guid roomId, Guid senderId, string content)
    {
        var request = new SendMessageRequest
        {
            RoomId = roomId,
            SenderId = senderId,
            Type = MessageType.Text,
            Content = content
        };

        var response = await _client.PostAsJsonAsync("/chat/messages", request);
        response.EnsureSuccessStatusCode();
    }
}
