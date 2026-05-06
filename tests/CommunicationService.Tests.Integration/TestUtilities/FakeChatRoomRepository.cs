using CommunicationService.Application.Abstractions.Repositories;
using CommunicationService.Domain.Entities;

namespace CommunicationService.Tests.Integration.TestUtilities;

public sealed class FakeChatRoomRepository : IChatRoomRepository
{
    private readonly IReadOnlyList<ChatRoom> _expiredRooms;
    private readonly TaskCompletionSource<bool>? _getExpiredSignal;

    public FakeChatRoomRepository(
        IReadOnlyList<ChatRoom> expiredRooms,
        TaskCompletionSource<bool>? getExpiredSignal = null)
    {
        _expiredRooms = expiredRooms;
        _getExpiredSignal = getExpiredSignal;
    }

    public int SaveChangesCalls { get; private set; }

    public Task AddAsync(ChatRoom room, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task<ChatRoom?> GetByIdAsync(Guid roomId, CancellationToken cancellationToken)
    {
        return Task.FromResult<ChatRoom?>(null);
    }

    public Task<IReadOnlyList<ChatRoom>> GetExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        _getExpiredSignal?.TrySetResult(true);
        return Task.FromResult(_expiredRooms);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCalls++;
        return Task.CompletedTask;
    }
}
