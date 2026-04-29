using CommunicationService.Application.DTOs;
using CommunicationService.Domain.Entities;

namespace CommunicationService.Application.Mapping;

public static class ChatRoomMapper
{
    public static ChatRoomDto ToDto(ChatRoom room)
    {
        return new ChatRoomDto
        {
            Id = room.Id,
            IsTemporary = room.IsTemporary,
            ExpiresAt = room.ExpiresAt,
            IsExpired = room.IsExpired,
            CreatedAt = room.CreatedAt,
            Participants = room.Participants.Select(p => p.UserId).ToArray()
        };
    }
}
