using CommunicationService.Application.Abstractions.Time;

namespace CommunicationService.Tests.Integration.TestUtilities;

public sealed class FakeDateTimeProvider : IDateTimeProvider
{
    public FakeDateTimeProvider(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; private set; }

    public void Set(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public void Advance(TimeSpan delta)
    {
        UtcNow = UtcNow.Add(delta);
    }
}
