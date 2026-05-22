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
        _logger.LogInformation("IdentityEventStoreSubscriberWorker started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var checkpoint = await GetCheckpointAsync(stoppingToken);
                var startPosition = checkpoint >= 0
                    ? StreamPosition.FromInt64(checkpoint + 1)
                    : StreamPosition.Start;

                _logger.LogInformation("Subscribing to EventStore category stream {Stream} starting at position {Position}", CategoryStream, startPosition);

                bool hasEvents = false;

                try
                {
                    var events = _client.ReadStreamAsync(
                        Direction.Forwards,
                        CategoryStream,
                        startPosition,
                        resolveLinkTos: true,
                        cancellationToken: stoppingToken);

                    _logger.LogInformation("Connected to EventStore stream {Stream}, reading events...", CategoryStream);
                    await foreach (var resolvedEvent in events)
                    {
                        hasEvents = true;
                        // Use the original event number (position in the category stream) for checkpointing.
                        var eventNumber = (long)resolvedEvent.OriginalEventNumber.ToUInt64();
                        if (eventNumber <= checkpoint)
                        {
                            continue;
                        }

                        // Log raw event metadata before any filtering
                        try
                        {
                            var rawJson = System.Text.Encoding.UTF8.GetString(resolvedEvent.Event.Data.Span);
                            _logger.LogInformation("Raw event: Stream={StreamId} Type={EventType} Number={EventNumber} Payload={Payload}",
                                resolvedEvent.Event.EventStreamId,
                                resolvedEvent.Event.EventType,
                                eventNumber,
                                rawJson.Length > 200 ? rawJson[..200] + "..." : rawJson);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to read raw event payload for event {EventId}", resolvedEvent.Event.EventId);
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
        // Attempt to map/deserialize the event; log when unsupported or deserialization fails.
        IdentityUserChange? change;
        try
        {
            change = TryMapEvent(eventType, data);
        }
        catch (Exception ex)
        {
            var raw = string.Empty;
            try { raw = System.Text.Encoding.UTF8.GetString(data.Span); } catch { }
            _logger.LogError(ex, "Failed to deserialize event type {EventType}. Raw: {Raw}", eventType, raw.Length > 500 ? raw[..500] + "..." : raw);
            return;
        }

        if (change == null)
        {
            var raw = string.Empty;
            try { raw = System.Text.Encoding.UTF8.GetString(data.Span); } catch { }
            _logger.LogInformation("Skipping unsupported event type: {EventType}. RawPayload={Payload}", eventType, raw.Length > 200 ? raw[..200] + "..." : raw);
            return;
        }

        // FIX: Use a dedicated scope for UserShadow upsert.
        // This scope is disposed before room creation, ensuring a clean change tracker.
        bool userCreated;
        using (var userScope = _scopeFactory.CreateScope())
        {
            var dbContext = userScope.ServiceProvider.GetRequiredService<CommunicationDbContext>();

            var user = await dbContext.UserShadows
                .FirstOrDefaultAsync(u => u.Id == change.UserId, cancellationToken);

            userCreated = false;
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
                userCreated = true;
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

            if (userCreated)
            {
                _logger.LogInformation("Created UserShadow for {UserId} (Email={Email}, Role={Role})", change.UserId, change.Email, change.Role);
                _logger.LogInformation("Saving new UserShadow to database for {UserId}", change.UserId);
                try
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Saved UserShadow for {UserId} successfully", change.UserId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save new UserShadow for {UserId}. InnerException: {Inner}", change.UserId, ex.InnerException?.Message);
                    return;
                }
            }
            else
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        // userScope is now disposed — change tracker from UserShadow save is gone.

        // FIX: Room creation uses a FRESH scope, completely separate from UserShadow scope.
        // This eliminates any EF change tracker state pollution that could cause FK order issues.
        if (userCreated && IsUserRegistrationEvent(eventType))
        {
            _logger.LogInformation("Received UserRegistered event for {UserId}, starting room creation flow", change.UserId);
            try
            {
                await EnsureCustomerServiceRoomAsync(change.UserId, change.OccurredAt, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error during EnsureCustomerServiceRoomAsync for {UserId}. StackTrace: {StackTrace}", change.UserId, ex.StackTrace);
            }
            _logger.LogInformation("Finished room creation attempt for {UserId}", change.UserId);
        }
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

    private static bool IsUserRegistrationEvent(string eventType)
        => eventType == "UserRegisteredEvent" || eventType == "UserRegisteredEvent_v1";

    /// <summary>
    /// Creates its OWN fresh scope and dbContext — completely isolated from UserShadow save scope.
    ///
    /// FIX: Uses dbContext.ChatParticipants.AddRange() instead of room.AddParticipant() to avoid
    /// EF Core re-marking the already-saved ChatRoom as Modified (which causes DbUpdateConcurrencyException
    /// on the second SaveChangesAsync when EF Core batches an unexpected UPDATE for the room).
    /// </summary>
    private async Task EnsureCustomerServiceRoomAsync(
        Guid userId,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        // Fresh scope = clean change tracker, no leftover tracked entities
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CommunicationDbContext>();

        try
        {
            // Step 1: Verify UserShadow actually exists in DB (sanity check)
            _logger.LogInformation("[RoomCreate] Step 1: Verifying UserShadow exists for UserId={UserId}", userId);
            var shadowExists = await dbContext.UserShadows
                .AnyAsync(u => u.Id == userId, cancellationToken);

            if (!shadowExists)
            {
                _logger.LogError("[RoomCreate] UserShadow for {UserId} does NOT exist — aborting.", userId);
                return;
            }

            _logger.LogInformation("[RoomCreate] UserShadow confirmed for {UserId}", userId);

            // Step 2: Check for existing customer service room for this user (idempotency guard)
            _logger.LogInformation("[RoomCreate] Step 2: Checking existing room for UserId={UserId}", userId);
            var existingRoomCount = await dbContext.ChatRooms
                .Where(r => r.RoomType == ChatRoomType.CustomerService
                         && r.Participants.Any(p => p.ShadowUserId == userId))
                .CountAsync(cancellationToken);

            _logger.LogInformation("[RoomCreate] Existing CustomerService room count for {UserId}: {Count}", userId, existingRoomCount);

            if (existingRoomCount > 0)
            {
                _logger.LogInformation("[RoomCreate] Skipping — room already exists for {UserId}", userId);
                return;
            }

            // Step 3: Create and save the room FIRST (separate save to get the PK committed)
            _logger.LogInformation("[RoomCreate] Step 3: Creating ChatRoom for {UserId}", userId);
            var room = new ChatRoom(ChatRoomType.CustomerService, orderId: null, expiresAt: null, createdAt);
            var roomId = room.Id;

            dbContext.ChatRooms.Add(room);

            _logger.LogInformation("[RoomCreate] Step 4 (Room Save): Saving ChatRoom RoomId={RoomId}", roomId);
            try
            {
                var savedRoomRows = await dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("[RoomCreate] ChatRoom saved. Rows={Count}, RoomId={RoomId}", savedRoomRows, roomId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RoomCreate] FAILED saving ChatRoom {RoomId}. {ExType}: {Msg} | Inner: {Inner}",
                    roomId, ex.GetType().Name, ex.Message, ex.InnerException?.Message ?? "none");
                throw;
            }

            // Step 5: Query support users (admin/cs) — empty list is fine, room is created either way
            _logger.LogInformation("[RoomCreate] Step 5: Querying admin/cs users from users_shadow");
            var supportUserIds = await dbContext.UserShadows
                .Where(u => u.Role != null
                    && (u.Role.ToLower() == "cs" || u.Role.ToLower() == "admin")
                    && u.Id != userId)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("[RoomCreate] Support users found: {Count}", supportUserIds.Count);

            // Step 6: Build participant list and add DIRECTLY to DbSet.
            // IMPORTANT: Do NOT use room.AddParticipant() here — that mutates the tracked ChatRoom's
            // _participants backing field, which causes EF Core to mark ChatRoom as Modified and attempt
            // an UPDATE on the already-committed row, resulting in DbUpdateConcurrencyException.
            var participants = new List<ChatParticipant>
            {
                new ChatParticipant(roomId, userId, createdAt)
            };

            foreach (var supportUserId in supportUserIds)
            {
                _logger.LogInformation("[RoomCreate] Queuing support participant SupportUserId={SupportUserId} for RoomId={RoomId}", supportUserId, roomId);
                participants.Add(new ChatParticipant(roomId, supportUserId, createdAt));
            }

            _logger.LogInformation("[RoomCreate] Step 7 (Participants Save): Inserting {Count} participants for RoomId={RoomId}", participants.Count, roomId);
            dbContext.ChatParticipants.AddRange(participants);

            try
            {
                var savedParticipantRows = await dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("[RoomCreate] ChatParticipants saved. Rows={Count}, RoomId={RoomId}", savedParticipantRows, roomId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RoomCreate] FAILED saving ChatParticipants for RoomId={RoomId}. {ExType}: {Msg} | Inner: {Inner}",
                    roomId, ex.GetType().Name, ex.Message, ex.InnerException?.Message ?? "none");
                throw;
            }

            _logger.LogInformation("[RoomCreate] ✅ Done. RoomId={RoomId} UserId={UserId} Participants={Count}",
                roomId, userId, participants.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[RoomCreate] Unexpected exception for UserId={UserId}. {ExType}: {Msg} | Inner: {Inner}",
                userId, ex.GetType().Name, ex.Message, ex.InnerException?.Message ?? "none");
        }
    }

    private static string MapRole(int role)
    {
        return role switch
        {
            1 => "admin",
            2 => "customer",
            3 => "partner",
            4 => "cs",
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
