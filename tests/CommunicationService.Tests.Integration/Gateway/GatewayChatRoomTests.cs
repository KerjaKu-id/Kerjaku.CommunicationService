using System.Net;
using System.Net.Http.Json;
using CommunicationService.Api.Models;
using CommunicationService.Application.DTOs;
using CommunicationService.Application.DTOs.Requests;
using CommunicationService.Tests.Integration.TestUtilities;

namespace CommunicationService.Tests.Integration.Gateway;

[Collection("GatewayE2E")]
public class GatewayChatRoomTests
{
    private readonly HttpClient _client;
    private readonly GatewayDbHelper _db;

    public GatewayChatRoomTests(GatewayTestFixture fixture)
    {
        _client = fixture.Client;
        _db = new GatewayDbHelper(fixture);
    }

    [Fact]
    public async Task CreateRoom_PersistsRoomAndParticipants()
    {
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var request = new CreateChatRoomRequest
        {
            ParticipantIds = new[] { user1, user2 },
            IsTemporary = false
        };

        var response = await _client.PostAsJsonAsync("rooms", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<ChatRoomDto>>();
        Assert.NotNull(payload);

        var roomId = payload!.Data.Id;
        var room = await _db.GetChatRoomAsync(roomId);
        Assert.NotNull(room);
        Assert.False(room!.IsTemporary);

        var participantCount = await _db.CountParticipantsAsync(roomId);
        Assert.Equal(2, participantCount);
    }
}
