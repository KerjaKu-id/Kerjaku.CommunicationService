using CommunicationService.Application.DTOs;
using CommunicationService.Domain.Entities;

namespace CommunicationService.Application.Mapping;

public static class MessageStatusMapper
{
    public static MessageStatusDto ToDto(MessageStatus status, Guid roomId)
    {
        return new MessageStatusDto
        {
            MessageId = status.MessageId,
            RoomId = roomId,
            RecipientId = status.RecipientId,
            Status = status.Status,
            UpdatedAt = status.UpdatedAt
        };
    }
}
