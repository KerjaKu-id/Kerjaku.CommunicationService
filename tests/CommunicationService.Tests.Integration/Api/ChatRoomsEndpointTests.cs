using System.Net;
using System.Net.Http.Json;
using CommunicationService.Api.Models;
using CommunicationService.Application.DTOs;
using CommunicationService.Application.DTOs.Requests;
using CommunicationService.Tests.Integration.TestUtilities;

namespace CommunicationService.Tests.Integration.Api;

public class ChatRoomsEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ChatRoomsEndpointTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateRoom_ReturnsCreatedRoomWithLinks()
    {
        var request = new CreateChatRoomRequest
        {
            ParticipantIds = new[] { Guid.NewGuid(), Guid.NewGuid() },
            IsTemporary = false
        };

        var response = await _client.PostAsJsonAsync("/chat/rooms", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<ChatRoomDto>>();
        Assert.NotNull(payload);
        Assert.NotEqual(Guid.Empty, payload!.Data.Id);
        Assert.Equal(2, payload.Data.Participants.Count);
        Assert.Contains(payload.Links, link => link.Rel == "self");
    }
}
