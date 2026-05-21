// Communication.Application/IntegrationEventHandlers/UserRegisteredEventHandler.cs
// Place in: Kerjaku.CommunicationService/src/CommunicationService.Application/IntegrationEventHandlers/

using System;
using System.Threading;
using System.Threading.Tasks;
using CommunicationService.Domain.Entities;
using Kerjaku.Contracts.Communication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CommunicationService.Application.IntegrationEventHandlers;

/// <summary>
/// Generic interface for integration event handlers.
/// Implemented per event type for flexible DI registration.
/// </summary>
public interface IIntegrationEventHandler<TEvent> where TEvent : IntegrationEvent
{
    /// <summary>
    /// Handle the event. Must be idempotent (safe to call multiple times).
    /// </summary>
    Task HandleAsync(TEvent @event, CancellationToken ct = default);
}

/// <summary>
/// Handles UserRegisteredIntegrationEvent from Identity Service.
///
/// Responsibilities:
/// 1. Create ShadowUser in Communication database
/// 2. Create default support room for customer-service chat
/// 3. Add both user and CS representative as participants
///
/// Idempotency:
/// - If ShadowUser exists → skip creation
/// - If support room exists → return existing
/// - Safe to process same event multiple times
///
/// Event Flow:
/// Identity Service → UserRegisteredEvent → RabbitMQ
///   ↓
/// Communication Service Consumer → Handler → Creates Room
///   ↓
/// Frontend: GET /chat/rooms → Returns support room
///   ↓
/// SignalR: JoinRoom → Real-time chat ready
/// </summary>
public class UserRegisteredEventHandler : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    private readonly CommunicationDbContext _db;
    private readonly ILogger<UserRegisteredEventHandler> _logger;

    public UserRegisteredEventHandler(
        CommunicationDbContext db,
        ILogger<UserRegisteredEventHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Handle user registration: create shadow user + support room.
    /// Called by RabbitMQ consumer async.
    /// </summary>
    public async Task HandleAsync(
        UserRegisteredIntegrationEvent @event,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation(
                "Handling UserRegisteredEvent for user {UserId} ({Email}) with role {RoleId}",
                @event.UserId,
                @event.Email,
                @event.RoleId);

            // ─── STEP 1: Create or get ShadowUser (idempotent) ───────────────────
            var shadowUser = await _db.ShadowUsers
                .FirstOrDefaultAsync(u => u.Id == @event.UserId, cancellationToken: ct);

            if (shadowUser == null)
            {
                // Create shadow user
                shadowUser = new ShadowUser
                {
                    Id = @event.UserId,
                    Email = @event.Email,
                    DisplayName = @event.DisplayName,
                    AvatarUrl = @event.AvatarUrl,
                    Role = (UserRole)@event.RoleId,
                    CreatedAt = DateTime.UtcNow,
                };

                _db.ShadowUsers.Add(shadowUser);
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Created ShadowUser for {UserId}",
                    @event.UserId);
            }
            else
            {
                _logger.LogWarning(
                    "ShadowUser already exists for {UserId}, skipping creation",
                    @event.UserId);
            }

            // ─── STEP 2: Create support room (idempotent) ──────────────────────
            var supportRoom = await GetOrCreateSupportRoomAsync(@event.UserId, ct);

            _logger.LogInformation(
                "Successfully processed UserRegisteredEvent for {UserId}, room {RoomId}",
                @event.UserId,
                supportRoom.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling UserRegisteredEvent for user {UserId}",
                @event.UserId);
            throw; // Let consumer handle retry/DLQ
        }
    }

    /// <summary>
    /// Get or create support room for user.
    /// Idempotent: returns same room if called multiple times.
    ///
    /// Strategy: Upsert pattern
    /// 1. Check if support room exists for user
    /// 2. If exists → return
    /// 3. If not → create with both participants
    /// </summary>
    private async Task<ChatRoom> GetOrCreateSupportRoomAsync(
        Guid userId,
        CancellationToken ct)
    {
        // Find existing support room for this user
        var existingRoom = await _db.ChatRooms
            .Include(r => r.Participants)
            .FirstOrDefaultAsync(r =>
                r.RoomType == ChatRoomType.CustomerService &&
                r.OrderId == null &&
                r.Participants.Any(p => p.UserId == userId),
                cancellationToken: ct);

        if (existingRoom != null)
        {
            _logger.LogInformation(
                "Support room already exists for user {UserId}: {RoomId}",
                userId,
                existingRoom.Id);
            return existingRoom;
        }

        // Create new support room
        var room = new ChatRoom(
            roomType: ChatRoomType.CustomerService,
            orderId: null,
            expiresAt: null, // Support rooms never expire
            createdAt: DateTimeOffset.UtcNow);

        // Add user as participant
        room.AddParticipant(userId, DateTimeOffset.UtcNow);

        // Add CS representative as participant (if one exists)
        var csUser = await _db.ShadowUsers
            .FirstOrDefaultAsync(u => u.Role == UserRole.CS, cancellationToken: ct);

        if (csUser != null)
        {
            room.AddParticipant(csUser.Id, DateTimeOffset.UtcNow);
            _logger.LogInformation(
                "Added CS user {CsUserId} to support room",
                csUser.Id);
        }
        else
        {
            _logger.LogWarning(
                "No CS representative found in system, support room created with only user {UserId}",
                userId);
        }

        // Persist room
        _db.ChatRooms.Add(room);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created support room {RoomId} for user {UserId}",
            room.Id,
            userId);

        return room;
    }
}
