namespace CommunicationService.Domain.Entities;

/// <summary>
/// ChatParticipant (Join Entity / Association Table)
/// 
/// Represents the relationship between a ShadowUser and a ChatRoom.
/// 
/// DDD Pattern: Entity within ChatRoom aggregate boundary.
/// Not an aggregate root - owned by ChatRoom aggregate.
/// Multiple participations can exist (user in multiple rooms).
/// 
/// Design Rationale:
/// - Separate entity to track per-user state within each room
/// - Enables different permissions per user per room
/// - Tracks individual read status, join/leave times
/// - Supports efficient queries: "Which users are in this room?"
/// - Supports efficient queries: "Which rooms is this user in?"
/// 
/// Important: ShadowUserId is a FK reference to ShadowUser.
/// We store the ID directly in constructor for immutability.
/// Navigation property ShadowUser loads the actual user data when needed.
/// </summary>
public class ChatParticipant
{
    private ChatParticipant()
    {
    }

    public ChatParticipant(
        Guid chatRoomId,
        Guid shadowUserId,
        DateTimeOffset joinedAt,
        bool isAdmin = false)
    {
        Id = Guid.NewGuid();
        ChatRoomId = chatRoomId;
        ShadowUserId = shadowUserId;
        JoinedAt = joinedAt;
        IsAdmin = isAdmin;
    }

    /// <summary>Unique identifier for this participation record</summary>
    public Guid Id { get; private set; }
    
    /// <summary>FK: Which chat room this user joined</summary>
    public Guid ChatRoomId { get; private set; }
    
    /// <summary>
    /// FK: Reference to ShadowUser (previously called UserId for clarity).
    /// Uses Guid directly to maintain aggregate boundary.
    /// Populate ShadowUser navigation for user context.
    /// </summary>
    public Guid ShadowUserId { get; private set; }
    
    /// <summary>When user joined this room. Used for sorting messages by participant context.</summary>
    public DateTimeOffset JoinedAt { get; private set; }
    
    /// <summary>When user left the room (null = still active)</summary>
    public DateTimeOffset? LeftAt { get; private set; }
    
    /// <summary>
    /// Can this user post messages / manage the room?
    /// False = read-only or restricted participation
    /// </summary>
    public bool IsAdmin { get; private set; }
    
    /// <summary>
    /// When user last read messages in this room.
    /// Used to show unread count: filter messages after LastReadAt.
    /// </summary>
    public DateTimeOffset? LastReadAt { get; private set; }

    // ─── Navigation Properties ───────────────────────────────────────
    /// <summary>Back-reference to the chat room (required)</summary>
    public ChatRoom ChatRoom { get; private set; } = null!;
    
    /// <summary>
    /// Reference to the ShadowUser (required).
    /// Loads user display name, role, avatar for UI rendering.
    /// </summary>
    public ShadowUser ShadowUser { get; private set; } = null!;

    // ─── Business Methods ─────────────────────────────────────────
    /// <summary>Mark that user has read up to this point. Used to calculate unread count.</summary>
    public void UpdateLastRead(DateTimeOffset readAt)
    {
        LastReadAt = readAt;
    }
    
    /// <summary>Mark user as left the room (soft delete pattern).</summary>
    public void Leave(DateTimeOffset leftAt)
    {
        LeftAt = leftAt;
    }
    
    /// <summary>Check if user is still actively in the room.</summary>
    public bool IsActive() => LeftAt == null;
}
