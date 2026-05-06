using System.Collections.Concurrent;
using CommunicationService.Application.Abstractions.Events;

namespace CommunicationService.Tests.Integration.TestUtilities;

public sealed class TestEventPublisher : IEventPublisher
{
    private readonly ConcurrentQueue<object> _events = new();

    public IReadOnlyCollection<object> Published => _events.ToArray();

    public Task PublishAsync<TEvent>(TEvent eventData, CancellationToken cancellationToken)
    {
        _events.Enqueue(eventData!);
        return Task.CompletedTask;
    }
}
