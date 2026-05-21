using CommunicationService.Domain.Enums;

namespace CommunicationService.Domain.Entities;

/// <summary>\
/// Message: Entity representing a single message in a chat room.
/// 
/// DDD Pattern: Entity within ChatRoom aggregate boundary.
/// Not an aggregate root - owned by ChatRoom.
/// Immutable after creation (edited messages kept for audit).
/// 
/// Design Rationale:
/// - Belongs to ChatRoom aggregate (deleted with room)
/// - SenderId is FK to ShadowUser (never orphaned)
/// - MessageStatus tracks per-recipient delivery state
/// - Supports editing via UpdatedAt (soft update pattern)
/// - Metadata field for future attachment/rich content support
/// 
/// Advanced Feature:
/// - MessageStatus collection tracks "delivered to X", "read by Y"
/// - Enables read receipts, delivery confirmation
/// - Can query: "Which messages have user Z read?"
/// 
/// Audit Trail:
/// - CreatedAt: When message was initially sent
/// - UpdatedAt: When message was edited (if edited)
/// - IsDeleted: Soft delete pattern - keep message history
/// - DeletedAt: When message was deleted
/// </summary>
public class Message
{
    private readonly List<MessageStatus> _statuses = new();

    private Message()
    {
    }

    public Message(
        Guid chatRoomId,
        Guid senderId,
        MessageType type,
        string content,
        DateTimeOffset createdAt,
        string? metadata = null)
    {
        Id = Guid.NewGuid();
        ChatRoomId = chatRoomId;
        SenderId = senderId;
        Type = type;
        Content = content;
        Metadata = metadata;
        CreatedAt = createdAt;
        IsDeleted = false;
    }

    /// <summary>Unique message identifier</summary>
    public Guid Id { get; private set; }
    
    /// <summary>FK: Which chat room this message belongs to. Always set.</summary>
    public Guid ChatRoomId { get; private set; }
    
    /// <summary>
    /// FK: Who sent this message (ShadowUserId).
    /// Ensures message always has valid sender (referential integrity).
    /// </summary>
    public Guid SenderId { get; private set; }
    
    /// <summary>Message type: Text, Image, System, Invoice</summary>
    public MessageType Type { get; private set; }
    
    /// <summary>The message content. Can be plain text, JSON, markdown, etc.</summary>
    public string Content { get; private set; } = string.Empty;
    
    /// <summary>
    /// JSON metadata for rich content.
    /// Future: Attachment info, mentions, formatting, etc.
    /// </summary>
    public string? Metadata { get; private set; }
    
    /// <summary>When message was originally sent</summary>
    public DateTimeOffset CreatedAt { get; private set; }
    
    /// <summary>When message was edited (null if never edited)</summary>
    public DateTimeOffset? UpdatedAt { get; private set; }
    
    /// <summary>Soft delete flag. Never physically delete (audit trail).</summary>
    public bool IsDeleted { get; private set; }
    
    /// <summary>When message was deleted/retracted</summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    // ─── Navigation Properties ───────────────────────────────────────
    /// <summary>The chat room this message belongs to</summary>
    public ChatRoom ChatRoom { get; private set; } = null!;
    
    /// <summary>
    /// The user who sent this message.
    /// Loaded from ShadowUser to show display name, avatar, role.
    /// </summary>
    public ShadowUser Sender { get; private set; } = null!;
    
    /// <summary>
    /// Delivery status per recipient.
    /// Enables: read receipts, delivery confirmation, notifications.
    /// Query: "Has user X read this message?"
    /// </summary>
    public IReadOnlyCollection<MessageStatus> Statuses => _statuses;

    // ─── Business Methods ─────────────────────────────────────────
    /// <summary>Add delivery status for a specific recipient</summary>
    public void AddStatus(Guid recipientId, MessageDeliveryStatus status, DateTimeOffset updatedAt)
    {
        // Prevent duplicate status entries
        if (_statuses.Any(s => s.RecipientId == recipientId && s.Status == status))
            return;

        _statuses.Add(new MessageStatus(Id, recipientId, status, updatedAt));
    }
    
    /// <summary>Edit the message content (updates UpdatedAt)</summary>
    public void Edit(string newContent, DateTimeOffset editedAt)
    {
        if (IsDeleted)
            throw new InvalidOperationException("Cannot edit a deleted message");

        Content = newContent;
        UpdatedAt = editedAt;
    }
    
    /// <summary>Soft delete the message (keeps audit trail)</summary>
    public void Delete(DateTimeOffset deletedAt)
    {
        IsDeleted = true;
        DeletedAt = deletedAt;
        Content = "[Deleted]"; // Keep message in timeline for context
    }
    
    /// <summary>Check if message can still be edited (e.g., within 15 min window)</summary>
    public bool CanEdit(DateTimeOffset now, TimeSpan editWindow)
    {
        return !IsDeleted && (now - CreatedAt) < editWindow;
    }
}
