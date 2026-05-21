using CommunicationService.Domain.Entities;

namespace CommunicationService.Application.Abstractions.Repositories;

public interface IUserShadowRepository
{
    Task<UserShadow?> GetByIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, UserShadow>> GetByIdsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken);
    Task AddAsync(UserShadow user, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
