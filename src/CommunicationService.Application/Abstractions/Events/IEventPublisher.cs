namespace CommunicationService.Application.Abstractions.Events;

public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent eventData, CancellationToken cancellationToken);
}
