namespace CommunicationService.Application.Abstractions.Time;

public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
