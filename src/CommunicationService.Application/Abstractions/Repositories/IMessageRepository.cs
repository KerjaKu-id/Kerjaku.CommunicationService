using CommunicationService.Application.DTOs;
using CommunicationService.Domain.Entities;

namespace CommunicationService.Application.Abstractions.Repositories;

public interface IMessageRepository
{
    Task AddAsync(Message message, CancellationToken cancellationToken);
    Task<Message?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken);
    Task<PagedResult<Message>> GetByRoomIdPagedAsync(Guid roomId, int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
