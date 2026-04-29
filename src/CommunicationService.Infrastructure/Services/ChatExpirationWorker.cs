using CommunicationService.Application.Abstractions.Events;
using CommunicationService.Application.Abstractions.Repositories;
using CommunicationService.Application.Abstractions.Time;
using CommunicationService.Application.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace CommunicationService.Infrastructure.Services;

public sealed class ChatExpirationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEventPublisher _eventPublisher;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<ChatExpirationWorker> _logger;
    private readonly TimeSpan _interval;

    public ChatExpirationWorker(
        IServiceScopeFactory scopeFactory,
        IEventPublisher eventPublisher,
        IDateTimeProvider dateTimeProvider,
        IOptions<ChatExpirationWorkerOptions> options,
        ILogger<ChatExpirationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _eventPublisher = eventPublisher;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
        _interval = TimeSpan.FromSeconds(Math.Max(10, options.Value.CheckIntervalSeconds));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = _dateTimeProvider.UtcNow;
                using var scope = _scopeFactory.CreateScope();
                var chatRoomRepository = scope.ServiceProvider.GetRequiredService<IChatRoomRepository>();
                var expiredRooms = await chatRoomRepository.GetExpiredAsync(now, stoppingToken);
                if (expiredRooms.Count > 0)
                {
                    foreach (var room in expiredRooms)
                    {
                        room.MarkExpired();
                        await _eventPublisher.PublishAsync(
                            new ChatExpiredEvent(room.Id, room.ExpiresAt ?? now),
                            stoppingToken);
                    }

                    await chatRoomRepository.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while expiring chat rooms.");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}
