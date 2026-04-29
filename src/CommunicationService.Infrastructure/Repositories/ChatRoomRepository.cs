using CommunicationService.Application.Abstractions.Repositories;
using CommunicationService.Domain.Entities;
using CommunicationService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CommunicationService.Infrastructure.Repositories;

public class ChatRoomRepository : IChatRoomRepository
{
    private readonly CommunicationDbContext _dbContext;

    public ChatRoomRepository(CommunicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(ChatRoom room, CancellationToken cancellationToken)
    {
        return _dbContext.ChatRooms.AddAsync(room, cancellationToken).AsTask();
    }

    public Task<ChatRoom?> GetByIdAsync(Guid roomId, CancellationToken cancellationToken)
    {
        return _dbContext.ChatRooms
            .AsNoTracking()
            .Include(room => room.Participants)
            .FirstOrDefaultAsync(room => room.Id == roomId, cancellationToken);
    }

    public async Task<IReadOnlyList<ChatRoom>> GetExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        return await _dbContext.ChatRooms
            .Where(room => !room.IsExpired && room.ExpiresAt != null && room.ExpiresAt <= now)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
