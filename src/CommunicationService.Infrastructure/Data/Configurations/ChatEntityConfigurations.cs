// CommunicationService.Infrastructure/Data/Configurations/ChatEntityConfigurations.cs
// Place in: Kerjaku.CommunicationService/src/CommunicationService.Infrastructure/Data/Configurations/

using CommunicationService.Domain.Entities;
using CommunicationService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunicationService.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core entity configurations for chat domain model.
/// 
/// Responsibilities:
/// - Define table names and schemas
/// - Configure primary keys, foreign keys
/// - Define column types and constraints
/// - Setup indexes for query optimization
/// - Configure cascade delete strategy
/// - Configure value conversions (e.g., enums to int)
/// 
/// Design Principle: Configuration tied to entity, not DbContext.
/// Use IEntityTypeConfiguration<T> for clean separation.
/// </summary>

/// <summary>
/// Shadow User configuration.
/// 
/// Table: ShadowUsers
/// Purpose: Replicated user data from Identity Service
/// 
/// Key Decisions:
/// - Primary key = Id (Guid from Identity Service)
/// - Email unique (no duplicate users)
/// - Role stored as int (enum mapping)
/// - No cascade delete for ShadowUser (messages/participations kept for audit)
/// </summary>
public class ShadowUserConfiguration : IEntityTypeConfiguration<ShadowUser>
{
    public void Configure(EntityTypeBuilder<ShadowUser> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(u => u.DisplayName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(u => u.AvatarUrl)
            .HasMaxLength(2048); // URL length limit

        builder.Property(u => u.Role)
            .HasConversion<int>() // Store enum as int
            .HasDefaultValue(UserRole.Customer);

        builder.Property(u => u.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        // ─── Indexes ─────────────────────────────────────────────────
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("IX_ShadowUsers_Email_Unique");

        builder.HasIndex(u => u.Role)
            .HasDatabaseName("IX_ShadowUsers_Role");

        builder.HasIndex(u => u.CreatedAt)
            .HasDatabaseName("IX_ShadowUsers_CreatedAt");

        // ─── Relationships ───────────────────────────────────────────
        // One-to-Many: ShadowUser -> Messages
        builder.HasMany(u => u.SentMessages)
            .WithOne(m => m.Sender)
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict); // Keep messages for audit trail

        // One-to-Many: ShadowUser -> ChatParticipants
        builder.HasMany(u => u.Participations)
            .WithOne(p => p.ShadowUser)
            .HasForeignKey(p => p.ShadowUserId)
            .OnDelete(DeleteBehavior.Restrict); // Keep participation records
    }
}

/// <summary>
/// Chat Room configuration.
/// 
/// Table: ChatRooms
/// Purpose: Container for conversations
/// 
/// Key Decisions:
/// - RoomType stored as int (enum mapping)
/// - OrderId nullable (only set for job-based chats)
/// - Composite index on (RoomType, OrderId) for fast lookup
/// - Support rooms identified by: RoomType=0 AND OrderId=null
/// - Cascade delete for participants and messages (cleanup on room delete)
/// </summary>
public class ChatRoomConfiguration : IEntityTypeConfiguration<ChatRoom>
{
    public void Configure(EntityTypeBuilder<ChatRoom> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.RoomType)
            .HasConversion<int>()
            .HasDefaultValue(ChatRoomType.CustomerService);

        builder.Property(r => r.Status)
            .HasConversion<int>()
            .HasDefaultValue(ChatRoomStatus.Active);

        builder.Property(r => r.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        // ─── Indexes ─────────────────────────────────────────────────
        // Fast lookup: Which support room does user belong to?
        builder.HasIndex(r => new { r.RoomType, r.OrderId })
            .HasDatabaseName("IX_ChatRooms_RoomType_OrderId");

        // Timeline query: Recent chats for user context
        builder.HasIndex(r => r.CreatedAt)
            .HasDatabaseName("IX_ChatRooms_CreatedAt");

        builder.HasIndex(r => r.Status)
            .HasDatabaseName("IX_ChatRooms_Status");

        builder.HasIndex(r => r.OrderId)
            .HasDatabaseName("IX_ChatRooms_OrderId");

        // ─── Relationships ───────────────────────────────────────────
        // One-to-Many: ChatRoom -> ChatParticipants
        builder.HasMany(r => r.Participants)
            .WithOne(p => p.ChatRoom)
            .HasForeignKey(p => p.ChatRoomId)
            .OnDelete(DeleteBehavior.Cascade); // Clean up participants when room deleted

        // One-to-Many: ChatRoom -> Messages
        builder.HasMany(r => r.Messages)
            .WithOne(m => m.ChatRoom)
            .HasForeignKey(m => m.ChatRoomId)
            .OnDelete(DeleteBehavior.Cascade); // Clean up messages when room deleted
    }
}

/// <summary>
/// Chat Participant configuration.
/// 
/// Table: ChatRoomParticipants (or ChatParticipants)
/// Purpose: Track user membership in each room
/// 
/// Key Decisions:
/// - Composite key could be (ChatRoomId, ShadowUserId), but using Id for flexibility
/// - Unique index on (ChatRoomId, ShadowUserId) to prevent duplicates
/// - LeftAt nullable: null = active, value = left the room
/// - LastReadAt used to calculate unread count
/// - IsAdmin tracks per-room permissions (user might be admin in one room, not another)
/// </summary>
public class ChatParticipantConfiguration : IEntityTypeConfiguration<ChatParticipant>
{
    public void Configure(EntityTypeBuilder<ChatParticipant> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.IsAdmin)
            .HasDefaultValue(false);

        builder.Property(p => p.JoinedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        // ─── Indexes ─────────────────────────────────────────────────
        // Prevent duplicate participations
        builder.HasIndex(p => new { p.ChatRoomId, p.ShadowUserId })
            .IsUnique()
            .HasDatabaseName("IX_ChatParticipants_RoomUser_Unique");

        // Query: "Which rooms is this user in?"
        builder.HasIndex(p => p.ShadowUserId)
            .HasDatabaseName("IX_ChatParticipants_ShadowUserId");

        // Query: "Who is in this room?" + "Filter active participants"
        builder.HasIndex(p => new { p.ChatRoomId, p.LeftAt })
            .HasDatabaseName("IX_ChatParticipants_Room_Active");

        // Query: "Show last read timestamp for unread calculation"
        builder.HasIndex(p => p.LastReadAt)
            .HasDatabaseName("IX_ChatParticipants_LastReadAt");

        // ─── Relationships ───────────────────────────────────────────
        // Foreign keys configured in ChatRoom and ShadowUser configs
        // This avoids circular reference during configuration
    }
}

/// <summary>
/// Message configuration.
/// 
/// Table: Messages
/// Purpose: Store individual messages in conversations
/// 
/// Key Decisions:
/// - MessageType stored as int (enum mapping)
/// - IsDeleted flag for soft delete (never physically delete)
/// - DeletedAt nullable: null = active, value = when deleted
/// - Metadata JSON for future rich content support
/// - Content max 4000 chars (reasonable chat message limit)
/// - Cascade delete with ChatRoom (all messages deleted with room)
/// - Restrict delete for ShadowUser (keep message history for audit)
/// </summary>
public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type)
            .HasConversion<int>()
            .HasDefaultValue(MessageType.Text);

        builder.Property(m => m.Content)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(m => m.Metadata)
            .HasMaxLength(2000); // JSON metadata size limit

        builder.Property(m => m.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(m => m.IsDeleted)
            .HasDefaultValue(false);

        // ─── Indexes ─────────────────────────────────────────────────
        // Load messages for a room in reverse chronological order
        builder.HasIndex(m => new { m.ChatRoomId, m.CreatedAt })
            .HasDatabaseName("IX_Messages_Room_CreatedAt");

        // Find all messages from a user (audit, user activity)
        builder.HasIndex(m => m.SenderId)
            .HasDatabaseName("IX_Messages_SenderId");

        // Filter active vs deleted messages
        builder.HasIndex(m => m.IsDeleted)
            .HasDatabaseName("IX_Messages_IsDeleted");

        // Find messages within time window (for edits, deletes)
        builder.HasIndex(m => m.UpdatedAt)
            .HasDatabaseName("IX_Messages_UpdatedAt");

        // ─── Relationships ───────────────────────────────────────────
        // Many-to-One: Message -> ChatRoom
        // Handled by ChatRoom config (HasMany)

        // Many-to-One: Message -> ShadowUser (Sender)
        // Handled by ShadowUser config (HasMany -> SentMessages)

        // One-to-Many: Message -> MessageStatus
        builder.HasMany<MessageStatus>() // Can't reference directly if not loading
            .WithOne(s => s.Message)
            .HasForeignKey(s => s.MessageId)
            .OnDelete(DeleteBehavior.Cascade); // Clean up status with message
    }
}

/// <summary>
/// Message Status configuration.
/// 
/// Table: MessageStatuses
/// Purpose: Track delivery status per recipient
/// 
/// Enables: Read receipts, delivery confirmation, notifications
/// 
/// Key Decisions:
/// - No explicit entity class (value object), but EF needs config
/// - RecipientId is Guid to ShadowUser (FK not enforced - prevents cascade issues)
/// - Status stored as int
/// - Cascade delete with Message (clean up with message)
/// </summary>
public class MessageStatusConfiguration : IEntityTypeConfiguration<MessageStatus>
{
    public void Configure(EntityTypeBuilder<MessageStatus> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Status)
            .HasConversion<int>()
            .HasDefaultValue(MessageDeliveryStatus.Sent);

        builder.Property(s => s.UpdatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        // ─── Indexes ─────────────────────────────────────────────────
        // Query: "Has user X read this message?"
        builder.HasIndex(s => new { s.MessageId, s.RecipientId })
            .HasDatabaseName("IX_MessageStatus_Message_Recipient");

        // Query: "Show read/delivered status by recipient"
        builder.HasIndex(s => s.Status)
            .HasDatabaseName("IX_MessageStatus_Status");

        // ─── NO circular FK ───────────────────────────────────────────
        // We do NOT create FK to ShadowUser here
        // Prevents cascade delete chain: Message -> MessageStatus -> ShadowUser
        // Instead, RecipientId is stored as Guid without FK constraint
    }
}
