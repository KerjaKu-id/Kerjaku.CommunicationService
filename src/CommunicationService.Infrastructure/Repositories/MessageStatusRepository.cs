using CommunicationService.Application.Abstractions.Repositories;
using CommunicationService.Domain.Entities;
using CommunicationService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CommunicationService.Infrastructure.Repositories;

public class MessageStatusRepository : IMessageStatusRepository
{
    private readonly CommunicationDbContext _dbContext;

    public MessageStatusRepository(CommunicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(MessageStatus status, CancellationToken cancellationToken)
    {
        return _dbContext.MessageStatuses.AddAsync(status, cancellationToken).AsTask();
    }

    public Task AddRangeAsync(IEnumerable<MessageStatus> statuses, CancellationToken cancellationToken)
    {
        return _dbContext.MessageStatuses.AddRangeAsync(statuses, cancellationToken);
    }

    public Task<MessageStatus?> GetAsync(Guid messageId, Guid recipientId, CancellationToken cancellationToken)
    {
        return _dbContext.MessageStatuses
            .FirstOrDefaultAsync(status => status.MessageId == messageId && status.RecipientId == recipientId, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
