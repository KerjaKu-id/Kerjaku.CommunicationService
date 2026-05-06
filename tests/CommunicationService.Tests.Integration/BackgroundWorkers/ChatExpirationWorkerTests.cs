using System.Reflection;
using CommunicationService.Application.Abstractions.Events;
using CommunicationService.Application.Abstractions.Repositories;
using CommunicationService.Application.Events;
using CommunicationService.Domain.Entities;
using CommunicationService.Infrastructure.Services;
using CommunicationService.Tests.Integration.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CommunicationService.Tests.Integration.BackgroundWorkers;

public class ChatExpirationWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_MarksExpiredRooms_AndPublishesEvent()
    {
        var now = new DateTimeOffset(2026, 4, 30, 12, 0, 0, TimeSpan.Zero);
        var room = new ChatRoom(true, now.AddMinutes(-5), now.AddHours(-1));
        var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new FakeChatRoomRepository(new[] { room }, signal);
        var publisher = new TestEventPublisher();

        var services = new ServiceCollection();
        services.AddScoped<IChatRoomRepository>(_ => repository);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var worker = new ChatExpirationWorker(
            scopeFactory,
            publisher,
            new FakeDateTimeProvider(now),
            Options.Create(new ChatExpirationWorkerOptions { CheckIntervalSeconds = 10 }),
            new LoggerFactory().CreateLogger<ChatExpirationWorker>());

        await worker.StartAsync(CancellationToken.None);
        await signal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync(CancellationToken.None);

        Assert.True(room.IsExpired);
        Assert.Equal(1, repository.SaveChangesCalls);
        Assert.Contains(publisher.Published.OfType<ChatExpiredEvent>(), evt => evt.RoomId == room.Id);
    }

    [Fact]
    public void Constructor_ClampsIntervalToMinimum()
    {
        var repository = new FakeChatRoomRepository(Array.Empty<ChatRoom>());
        var services = new ServiceCollection();
        services.AddScoped<IChatRoomRepository>(_ => repository);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var worker = new ChatExpirationWorker(
            scopeFactory,
            new TestEventPublisher(),
            new FakeDateTimeProvider(DateTimeOffset.UtcNow),
            Options.Create(new ChatExpirationWorkerOptions { CheckIntervalSeconds = 1 }),
            new LoggerFactory().CreateLogger<ChatExpirationWorker>());

        var intervalField = typeof(ChatExpirationWorker)
            .GetField("_interval", BindingFlags.Instance | BindingFlags.NonPublic);
        var interval = (TimeSpan)intervalField!.GetValue(worker)!;

        Assert.Equal(TimeSpan.FromSeconds(10), interval);
    }
}
