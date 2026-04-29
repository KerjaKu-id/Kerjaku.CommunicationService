using CommunicationService.Application.DTOs;
using CommunicationService.Domain.Entities;

namespace CommunicationService.Application.Mapping;

public static class MessageMapper
{
    public static MessageDto ToDto(Message message)
    {
        return new MessageDto
        {
            Id = message.Id,
            RoomId = message.ChatRoomId,
            SenderId = message.SenderId,
            Type = message.Type,
            Content = message.Content,
            CreatedAt = message.CreatedAt,
            Statuses = message.Statuses.Select(status => MessageStatusMapper.ToDto(status, message.ChatRoomId)).ToArray()
        };
    }
}
