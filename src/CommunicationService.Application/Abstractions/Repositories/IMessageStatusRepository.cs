using CommunicationService.Domain.Entities;

namespace CommunicationService.Application.Abstractions.Repositories;

public interface IMessageStatusRepository
{
    Task AddAsync(MessageStatus status, CancellationToken cancellationToken);
    Task AddRangeAsync(IEnumerable<MessageStatus> statuses, CancellationToken cancellationToken);
    Task<MessageStatus?> GetAsync(Guid messageId, Guid recipientId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
