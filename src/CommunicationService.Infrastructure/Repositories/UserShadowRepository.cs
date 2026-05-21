using CommunicationService.Application.Abstractions.Repositories;
using CommunicationService.Domain.Entities;
using CommunicationService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CommunicationService.Infrastructure.Repositories;

public class UserShadowRepository : IUserShadowRepository
{
    private readonly CommunicationDbContext _dbContext;

    public UserShadowRepository(CommunicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<UserShadow?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return _dbContext.UserShadows
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, UserShadow>> GetByIdsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, UserShadow>();
        }

        var users = await _dbContext.UserShadows
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .ToListAsync(cancellationToken);

        return users.ToDictionary(user => user.Id, user => user);
    }

    public Task AddAsync(UserShadow user, CancellationToken cancellationToken)
    {
        return _dbContext.UserShadows.AddAsync(user, cancellationToken).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
