namespace CommunicationService.Domain.Entities;

public class EventStoreCheckpoint
{
    public string Name { get; set; } = string.Empty;
    public long LastEventNumber { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
