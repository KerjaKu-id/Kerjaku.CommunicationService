using CommunicationService.Domain.Entities;

namespace CommunicationService.Application.Abstractions.Repositories;

public interface IChatRoomRepository
{
    Task AddAsync(ChatRoom room, CancellationToken cancellationToken);
    Task<ChatRoom?> GetByIdAsync(Guid roomId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ChatRoom>> GetExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
