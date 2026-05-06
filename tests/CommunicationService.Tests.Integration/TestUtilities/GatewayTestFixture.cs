using System.Net.Http;
using CommunicationService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CommunicationService.Tests.Integration.TestUtilities;

public sealed class GatewayTestFixture : IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly TimeSpan _startupTimeout;
    private readonly TimeSpan _pollInterval;

    public GatewayTestFixture()
    {
        ChatBaseUri = ResolveChatBaseUri();
        ConnectionString = ResolveConnectionString();

        _startupTimeout = TimeSpan.FromSeconds(GetInt("GATEWAY_STARTUP_TIMEOUT_SECONDS", 30));
        _pollInterval = TimeSpan.FromSeconds(GetInt("GATEWAY_POLL_INTERVAL_SECONDS", 2));

        _client = new HttpClient
        {
            BaseAddress = ChatBaseUri,
            Timeout = TimeSpan.FromSeconds(GetInt("GATEWAY_HTTP_TIMEOUT_SECONDS", 15))
        };
    }

    public Uri ChatBaseUri { get; }
    public string ConnectionString { get; }

    public HttpClient Client => _client;

    public TimeSpan ExpirationWaitTimeout =>
        TimeSpan.FromSeconds(GetInt("CHAT_EXPIRATION_WAIT_SECONDS", 90));

    public CommunicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CommunicationDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new CommunicationDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await WaitForGatewayAsync();
        await WaitForDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    public async Task<bool> WaitForConditionAsync(
        Func<CommunicationDbContext, Task<bool>> predicate,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var db = CreateDbContext();
            if (await predicate(db))
            {
                return true;
            }

            await Task.Delay(_pollInterval);
        }

        return false;
    }

    private async Task WaitForGatewayAsync()
    {
        var deadline = DateTimeOffset.UtcNow.Add(_startupTimeout);
        Exception? lastException = null;
        var healthUri = new Uri(ChatBaseUri, "/health");

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using var response = await _client.GetAsync(healthUri);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
            }

            await Task.Delay(_pollInterval);
        }

        throw new InvalidOperationException(
            $"Gateway not reachable at {healthUri}.",
            lastException);
    }

    private async Task WaitForDatabaseAsync()
    {
        var deadline = DateTimeOffset.UtcNow.Add(_startupTimeout);
        Exception? lastException = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                await using var db = CreateDbContext();
                if (await db.Database.CanConnectAsync())
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
            }

            await Task.Delay(_pollInterval);
        }

        throw new InvalidOperationException(
            "SQL Server not reachable for integration tests.",
            lastException);
    }

    private static Uri ResolveChatBaseUri()
    {
        var baseUrl = Environment.GetEnvironmentVariable("GATEWAY_BASE_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "http://localhost/chat/";
        }

        if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
        {
            baseUrl += "/";
        }

        return new Uri(baseUrl, UriKind.Absolute);
    }

    private static string ResolveConnectionString()
    {
        var connection = Environment.GetEnvironmentVariable("COMMUNICATION_DB_CONNECTION");
        if (!string.IsNullOrWhiteSpace(connection))
        {
            return connection;
        }

        return "Server=localhost,1433;Database=CommunicationDb;User Id=sa;Password=Your_password123;TrustServerCertificate=True;Encrypt=False;";
    }

    private static int GetInt(string name, int fallback)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (int.TryParse(raw, out var value) && value > 0)
        {
            return value;
        }

        return fallback;
    }
}
