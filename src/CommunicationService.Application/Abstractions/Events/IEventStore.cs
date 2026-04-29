namespace CommunicationService.Application.Abstractions.Events;

public interface IEventStore
{
    Task AppendAsync<TEvent>(TEvent eventData, CancellationToken cancellationToken);
}
