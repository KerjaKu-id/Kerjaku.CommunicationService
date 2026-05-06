using CommunicationService.Application.Abstractions.Events;
using CommunicationService.Application.Abstractions.Time;
using CommunicationService.Infrastructure.Data;
using CommunicationService.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CommunicationService.Tests.Integration.TestUtilities;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"CommunicationServiceTests_{Guid.NewGuid():N}";

    public FakeDateTimeProvider DateTimeProvider { get; } =
        new(new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero));

    public TestEventPublisher EventPublisher { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var dbDescriptor = services.SingleOrDefault(
                descriptor => descriptor.ServiceType == typeof(DbContextOptions<CommunicationDbContext>));
            if (dbDescriptor != null)
            {
                services.Remove(dbDescriptor);
            }

            services.AddDbContext<CommunicationDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            var publisherDescriptor = services.SingleOrDefault(
                descriptor => descriptor.ServiceType == typeof(IEventPublisher));
            if (publisherDescriptor != null)
            {
                services.Remove(publisherDescriptor);
            }

            services.AddSingleton<IEventPublisher>(EventPublisher);

            var dateDescriptor = services.SingleOrDefault(
                descriptor => descriptor.ServiceType == typeof(IDateTimeProvider));
            if (dateDescriptor != null)
            {
                services.Remove(dateDescriptor);
            }

            services.AddSingleton<IDateTimeProvider>(DateTimeProvider);

            var hostedDescriptor = services.SingleOrDefault(
                descriptor => descriptor.ImplementationType == typeof(ChatExpirationWorker));
            if (hostedDescriptor != null)
            {
                services.Remove(hostedDescriptor);
            }
        });
    }
}
