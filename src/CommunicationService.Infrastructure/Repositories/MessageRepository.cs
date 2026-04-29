using CommunicationService.Application.Abstractions.Repositories;
using CommunicationService.Application.DTOs;
using CommunicationService.Domain.Entities;
using CommunicationService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CommunicationService.Infrastructure.Repositories;

public class MessageRepository : IMessageRepository
{
    private readonly CommunicationDbContext _dbContext;

    public MessageRepository(CommunicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(Message message, CancellationToken cancellationToken)
    {
        return _dbContext.Messages.AddAsync(message, cancellationToken).AsTask();
    }

    public Task<Message?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken)
    {
        return _dbContext.Messages
            .AsNoTracking()
            .FirstOrDefaultAsync(message => message.Id == messageId, cancellationToken);
    }

    public async Task<PagedResult<Message>> GetByRoomIdPagedAsync(
        Guid roomId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var skip = (pageNumber - 1) * pageSize;
        var results = await _dbContext.Messages
            .AsNoTracking()
            .Include(message => message.Statuses)
            .Where(message => message.ChatRoomId == roomId)
            .OrderBy(message => message.CreatedAt)
            .Skip(skip)
            .Take(pageSize + 1)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var hasNext = results.Count > pageSize;
        var items = results.Take(pageSize).ToArray();

        return new PagedResult<Message>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            HasNext = hasNext
        };
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
