// CommunicationService.Domain/Entities/ShadowUser.cs
// Place in: Kerjaku.CommunicationService/src/CommunicationService.Domain/Entities/

using System;
using System.Collections.Generic;
using CommunicationService.Domain.Enums;

namespace CommunicationService.Domain.Entities;

/// <summary>
/// Shadow User: Replicated user profile owned by Communication Service.
/// 
/// DDD Pattern: Read-model shadow copy (Event-sourced projection)
/// Synced from Identity Service via UserRegisteredIntegrationEvent.
/// 
/// Design Rationale:
/// - Communication Service owns this data independently (loose coupling)
/// - Avoids hard runtime dependency on Identity Service API/database
/// - Can be optimized with read replicas for high-volume queries
/// - Event-sourced: always in sync via domain events
/// - Enables Service-to-Service decoupling in distributed architecture
/// 
/// NOT an aggregate root - owned data of Communication Service.
/// Updated reactively when Identity Service publishes events.
/// 
/// Usage:
/// - Store minimal user info needed for chat context
/// - Primary key = UserId from Identity Service (maintains consistency)
/// - No circular dependencies back to Identity Service
/// </summary>
public class ShadowUser
{
    /// <summary>
    /// Unique identifier - matches Identity Service UserId for consistency.
    /// Prevents duplicate shadow users across services.
    /// </summary>
    public Guid Id { get; set; }
    
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    
    /// <summary>
    /// User role: determines permissions in chat (Admin, CS rep, Customer, Partner)
    /// </summary>
    public UserRole Role { get; set; }
    
    /// <summary>
    /// When this shadow copy was created/synced from Identity Service
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// When this shadow copy was last updated via domain events
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    // ─── Navigation Properties ───────────────────────────────────────
    // These are ONE-TO-MANY relationships OUTBOUND from ShadowUser.
    // These collections are used for:
    // - Queries: "Find all messages from user X"
    // - Queries: "Find all chats user X participates in"
    // - Cascading deletes: When shadow user removed, mark messages/participation
    
    /// <summary>
    /// All messages sent by this user.
    /// Caution: Can be large collection in high-activity users.
    /// Query only in specific contexts, not on every user load.
    /// </summary>
    public ICollection<Message> SentMessages { get; set; } = new List<Message>();
    
    /// <summary>
    /// All chat room participations (join entity).
    /// Used to find "which chats does this user belong to?"
    /// </summary>
    public ICollection<ChatParticipant> Participations { get; set; } = new List<ChatParticipant>();
}
