using CommunicationService.Application.DTOs;
using CommunicationService.Domain.Entities;

namespace CommunicationService.Application.Mapping;

public static class MessageMapper
{
    public static MessageDto ToDto(
        Message message,
        string messageType,
        object? metadata,
        string? senderName,
        string? senderAvatar)
    {
        return new MessageDto
        {
            Id = message.Id,
            RoomId = message.ChatRoomId,
            SenderId = message.SenderId,
            Type = message.Type,
            MessageType = messageType,
            Content = message.Content,
            Metadata = metadata,
            CreatedAt = message.CreatedAt,
            Statuses = message.Statuses.Select(status => MessageStatusMapper.ToDto(status, message.ChatRoomId)).ToArray(),
            SenderName = senderName,
            SenderAvatar = senderAvatar
        };
    }
}
