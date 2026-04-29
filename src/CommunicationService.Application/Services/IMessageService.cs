using CommunicationService.Application.DTOs;
using CommunicationService.Application.DTOs.Requests;

namespace CommunicationService.Application.Services;

public interface IMessageService
{
    Task<MessageDto> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken);
    Task<PagedResult<MessageDto>> GetMessagesAsync(Guid roomId, int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<MessageStatusDto> MarkMessageReadAsync(Guid messageId, Guid readerId, CancellationToken cancellationToken);
}
