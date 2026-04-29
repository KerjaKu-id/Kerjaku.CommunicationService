namespace CommunicationService.Infrastructure.Services;

public sealed class ChatExpirationWorkerOptions
{
    public int CheckIntervalSeconds { get; init; } = 60;
}
