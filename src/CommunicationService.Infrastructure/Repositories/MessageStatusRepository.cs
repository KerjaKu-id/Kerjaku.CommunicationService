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

    public async Task<int> CountUnreadByRoomAsync(Guid roomId, Guid recipientId, CancellationToken cancellationToken)
    {
        return await _dbContext.MessageStatuses
            .Join(
                _dbContext.Messages,
                status => status.MessageId,
                message => message.Id,
                (status, message) => new { status, message })
            .Where(entry => entry.message.ChatRoomId == roomId)
            .Where(entry => entry.status.RecipientId == recipientId)
            .Where(entry => entry.status.Status != CommunicationService.Domain.Enums.MessageDeliveryStatus.Read)
            .CountAsync(cancellationToken);
    }

    public async Task MarkRoomMessagesAsReadAsync(Guid roomId, Guid recipientId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // ─── READ RECEIPT DATABASE SYNC ───────────────────────────────────────
        // Fetch all unread message status entries matching the recipient and chat room,
        // then update each status directly to 'Read' status.
        var unreadStatuses = await _dbContext.MessageStatuses
            .Join(
                _dbContext.Messages,
                status => status.MessageId,
                message => message.Id,
                (status, message) => new { status, message })
            .Where(entry => entry.message.ChatRoomId == roomId)
            .Where(entry => entry.status.RecipientId == recipientId)
            .Where(entry => entry.status.Status != CommunicationService.Domain.Enums.MessageDeliveryStatus.Read)
            .Select(entry => entry.status)
            .ToListAsync(cancellationToken);

        foreach (var status in unreadStatuses)
        {
            status.UpdateStatus(CommunicationService.Domain.Enums.MessageDeliveryStatus.Read, now);
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
