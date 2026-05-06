using System.Net.Http.Json;
using CommunicationService.Api.Models;
using CommunicationService.Application.DTOs;
using CommunicationService.Application.DTOs.Requests;
using CommunicationService.Domain.Enums;

namespace CommunicationService.Tests.Integration.TestUtilities;

public sealed class GatewayApiClient
{
    private readonly HttpClient _client;

    public GatewayApiClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<ChatRoomDto> CreateRoomAsync(
        Guid user1,
        Guid user2,
        bool isTemporary = false,
        DateTimeOffset? expiresAt = null)
    {
        var request = new CreateChatRoomRequest
        {
            ParticipantIds = new[] { user1, user2 },
            IsTemporary = isTemporary,
            ExpiresAt = expiresAt
        };

        var response = await _client.PostAsJsonAsync("rooms", request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<ChatRoomDto>>();
        if (payload == null)
        {
            throw new InvalidOperationException("Room creation response was empty.");
        }

        return payload.Data;
    }

    public async Task<MessageDto> SendMessageAsync(Guid roomId, Guid senderId, string content)
    {
        var request = new SendMessageRequest
        {
            RoomId = roomId,
            SenderId = senderId,
            Type = MessageType.Text,
            Content = content
        };

        var response = await _client.PostAsJsonAsync("messages", request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<MessageDto>>();
        if (payload == null)
        {
            throw new InvalidOperationException("Send message response was empty.");
        }

        return payload.Data;
    }

    public async Task<ApiResponse<PagedResult<MessageDto>>> GetMessagesAsync(
        Guid roomId,
        int pageNumber,
        int pageSize)
    {
        var response = await _client.GetAsync(
            $"messages?roomId={roomId}&pageNumber={pageNumber}&pageSize={pageSize}");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<MessageDto>>>();
        if (payload == null)
        {
            throw new InvalidOperationException("Get messages response was empty.");
        }

        return payload;
    }
}
