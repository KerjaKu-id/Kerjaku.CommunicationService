using System.Text.Json;
using CommunicationService.Domain.Entities;
using CommunicationService.Infrastructure.Data;
using EventStore.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CommunicationService.Infrastructure.IdentitySync;

/// <summary>
/// Subscribes to Identity's EventStoreDB category stream and maintains a local user shadow table.
/// This enables chat to resolve display names and avatars without calling Identity on every request.
/// </summary>
public sealed class IdentityEventStoreSubscriberWorker : BackgroundService
{
    private const string CategoryStream = "$ce-user";
    private const string CheckpointName = "identity-user-shadow";
    private readonly EventStoreClient _client;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IdentityEventStoreSubscriberWorker> _logger;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public IdentityEventStoreSubscriberWorker(
        EventStoreClient client,
        IServiceScopeFactory scopeFactory,
        ILogger<IdentityEventStoreSubscriberWorker> logger)
    {
        _client = client;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var checkpoint = await GetCheckpointAsync(stoppingToken);
                var startPosition = checkpoint >= 0
                    ? StreamPosition.FromInt64(checkpoint + 1)
                    : StreamPosition.Start;

                bool hasEvents = false;

                try
                {
                    var events = _client.ReadStreamAsync(
                        Direction.Forwards,
                        CategoryStream,
                        startPosition,
                        resolveLinkTos: true,
                        cancellationToken: stoppingToken);

                    await foreach (var resolvedEvent in events)
                    {
                        hasEvents = true;
                        var eventNumber = (long)resolvedEvent.Event.EventNumber.ToUInt64();
                        if (eventNumber <= checkpoint)
                        {
                            continue;
                        }

                        await ApplyEventAsync(resolvedEvent.Event.EventType, resolvedEvent.Event.Data, stoppingToken);
                        await UpdateCheckpointAsync(eventNumber, stoppingToken);
                        checkpoint = eventNumber;
                    }
                }
                catch (StreamNotFoundException)
                {
                    // The category stream doesn't exist yet; retry later.
                }

                if (!hasEvents)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Identity EventStore subscriber failed. Retrying in 5 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ApplyEventAsync(string eventType, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        var change = TryMapEvent(eventType, data);
        if (change == null)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CommunicationDbContext>();

        var user = await dbContext.UserShadows
            .FirstOrDefaultAsync(u => u.Id == change.UserId, cancellationToken);

        if (user == null)
        {
            user = new UserShadow(
                change.UserId,
                change.Email ?? string.Empty,
                change.DisplayName,
                change.AvatarUrl,
                change.FirebaseUid,
                change.Role,
                change.Status,
                change.OccurredAt);
            dbContext.UserShadows.Add(user);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(change.Email))
            {
                user.UpdateEmail(change.Email, change.OccurredAt);
            }

            if (!string.IsNullOrWhiteSpace(change.DisplayName) || !string.IsNullOrWhiteSpace(change.AvatarUrl))
            {
                user.UpdateProfile(change.DisplayName, change.AvatarUrl, change.OccurredAt);
            }

            if (!string.IsNullOrWhiteSpace(change.Role))
            {
                user.UpdateRole(change.Role, change.OccurredAt);
            }

            if (!string.IsNullOrWhiteSpace(change.Status))
            {
                user.UpdateStatus(change.Status, change.OccurredAt);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IdentityUserChange? TryMapEvent(string eventType, ReadOnlyMemory<byte> data)
    {
        return eventType switch
        {
            "UserRegisteredEvent" => MapUserRegistered(JsonSerializer.Deserialize<UserRegisteredEvent>(data.Span, SerializerOptions)),
            "UserRegisteredEvent_v1" => MapUserRegisteredLegacy(JsonSerializer.Deserialize<UserRegisteredEventV1>(data.Span, SerializerOptions)),
            "UserProfileUpdatedEvent" => MapUserProfileUpdated(JsonSerializer.Deserialize<UserProfileUpdatedEvent>(data.Span, SerializerOptions)),
            "UserRoleAssignedEvent" => MapUserRoleAssigned(JsonSerializer.Deserialize<UserRoleAssignedEvent>(data.Span, SerializerOptions)),
            "UserRoleUpgradedDomainEvent" => MapUserRoleUpgraded(JsonSerializer.Deserialize<UserRoleUpgradedDomainEvent>(data.Span, SerializerOptions)),
            "UserStatusChangedEvent" => MapUserStatusChanged(JsonSerializer.Deserialize<UserStatusChangedEvent>(data.Span, SerializerOptions)),
            _ => null
        };
    }

    private static IdentityUserChange? MapUserRegistered(UserRegisteredEvent? evt)
    {
        if (evt == null)
        {
            return null;
        }

        return new IdentityUserChange(
            evt.UserId,
            evt.Email,
            DisplayName: null,
            AvatarUrl: null,
            FirebaseUid: evt.FirebaseUid,
            Role: MapRole(evt.Role),
            Status: MapStatus(evt.Status),
            evt.OccurredAt);
    }

    private static IdentityUserChange? MapUserRegisteredLegacy(UserRegisteredEventV1? evt)
    {
        if (evt == null)
        {
            return null;
        }

        return new IdentityUserChange(
            evt.UserId,
            Email: string.Empty,
            DisplayName: null,
            AvatarUrl: null,
            FirebaseUid: evt.FirebaseUid,
            Role: MapRole(evt.Role),
            Status: MapStatus(evt.Status),
            evt.OccurredAt);
    }

    private static IdentityUserChange? MapUserProfileUpdated(UserProfileUpdatedEvent? evt)
    {
        if (evt == null)
        {
            return null;
        }

        var displayName = !string.IsNullOrWhiteSpace(evt.DisplayName)
            ? evt.DisplayName
            : evt.FullName;

        return new IdentityUserChange(
            evt.UserId,
            Email: null,
            displayName,
            AvatarUrl: evt.AvatarUrl,
            FirebaseUid: null,
            Role: null,
            Status: null,
            evt.OccurredAt);
    }

    private static IdentityUserChange? MapUserRoleAssigned(UserRoleAssignedEvent? evt)
    {
        if (evt == null)
        {
            return null;
        }

        return new IdentityUserChange(
            evt.UserId,
            Email: null,
            DisplayName: null,
            AvatarUrl: null,
            FirebaseUid: null,
            Role: MapRole(evt.Role),
            Status: null,
            evt.OccurredAt);
    }

    private static IdentityUserChange? MapUserRoleUpgraded(UserRoleUpgradedDomainEvent? evt)
    {
        if (evt == null)
        {
            return null;
        }

        return new IdentityUserChange(
            evt.UserId,
            Email: null,
            DisplayName: null,
            AvatarUrl: null,
            FirebaseUid: null,
            Role: MapRole(evt.NewRole),
            Status: null,
            evt.OccurredAt);
    }

    private static IdentityUserChange? MapUserStatusChanged(UserStatusChangedEvent? evt)
    {
        if (evt == null)
        {
            return null;
        }

        return new IdentityUserChange(
            evt.UserId,
            Email: null,
            DisplayName: null,
            AvatarUrl: null,
            FirebaseUid: null,
            Role: null,
            Status: MapStatus(evt.Status),
            evt.OccurredAt);
    }

    private async Task<long> GetCheckpointAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CommunicationDbContext>();
        var checkpoint = await dbContext.EventStoreCheckpoints.FindAsync([CheckpointName], cancellationToken);
        return checkpoint?.LastEventNumber ?? -1;
    }

    private async Task UpdateCheckpointAsync(long eventNumber, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CommunicationDbContext>();
        var checkpoint = await dbContext.EventStoreCheckpoints.FindAsync([CheckpointName], cancellationToken);

        if (checkpoint == null)
        {
            checkpoint = new EventStoreCheckpoint
            {
                Name = CheckpointName,
                LastEventNumber = eventNumber,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            dbContext.EventStoreCheckpoints.Add(checkpoint);
        }
        else
        {
            checkpoint.LastEventNumber = eventNumber;
            checkpoint.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string MapRole(int role)
    {
        return role switch
        {
            1 => "admin",
            2 => "customer",
            3 => "partner",
            _ => "customer"
        };
    }

    private static string MapStatus(int status)
    {
        return status switch
        {
            1 => "active",
            2 => "suspended",
            3 => "banned",
            _ => "active"
        };
    }

    private sealed record IdentityUserChange(
        Guid UserId,
        string? Email,
        string? DisplayName,
        string? AvatarUrl,
        string? FirebaseUid,
        string? Role,
        string? Status,
        DateTimeOffset OccurredAt);

    private sealed record UserRegisteredEvent(
        Guid UserId,
        string FirebaseUid,
        string Email,
        int Role,
        int Status,
        DateTimeOffset OccurredAt);

    private sealed record UserRegisteredEventV1(
        Guid UserId,
        string FirebaseUid,
        int Role,
        int Status,
        DateTimeOffset OccurredAt);

    private sealed record UserProfileUpdatedEvent(
        Guid UserId,
        string FullName,
        string? DisplayName,
        string? PhoneNumber,
        string? Address,
        string? AvatarUrl,
        DateTimeOffset OccurredAt);

    private sealed record UserRoleAssignedEvent(
        Guid UserId,
        int Role,
        DateTimeOffset OccurredAt);

    private sealed record UserRoleUpgradedDomainEvent(
        Guid UserId,
        int NewRole,
        DateTimeOffset OccurredAt);

    private sealed record UserStatusChangedEvent(
        Guid UserId,
        int Status,
        DateTimeOffset OccurredAt);
}
