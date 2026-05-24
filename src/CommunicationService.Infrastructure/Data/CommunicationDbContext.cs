using CommunicationService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CommunicationService.Infrastructure.Data;

public class CommunicationDbContext : DbContext
{
    public CommunicationDbContext(DbContextOptions<CommunicationDbContext> options) : base(options)
    {
    }

    public DbSet<ChatRoom> ChatRooms => Set<ChatRoom>();
    public DbSet<ChatParticipant> ChatParticipants => Set<ChatParticipant>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageStatus> MessageStatuses => Set<MessageStatus>();
    public DbSet<UserShadow> UserShadows => Set<UserShadow>();
    public DbSet<EventStoreCheckpoint> EventStoreCheckpoints => Set<EventStoreCheckpoint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatRoom>(entity =>
        {
            entity.ToTable("chat_rooms");
            entity.HasKey(room => room.Id);
            entity.Property(room => room.IsTemporary).IsRequired();
            entity.Property(room => room.IsExpired).IsRequired();
            entity.Property(room => room.CreatedAt).IsRequired();
            entity.Property(room => room.ExpiresAt);
            entity.Property(room => room.IsNegotiationActive).IsRequired().HasDefaultValue(false);
            entity.Property(room => room.NegotiationStatus).HasConversion<string>().IsRequired().HasDefaultValue(CommunicationService.Domain.Enums.NegotiationStatus.None);
            entity.Property(room => room.AgreedPrice).HasColumnType("decimal(18,2)");
            entity.HasIndex(room => room.ExpiresAt);
            entity.HasIndex(room => room.IsExpired);
            entity.HasMany(room => room.Participants)
                .WithOne(participant => participant.ChatRoom)
                .HasForeignKey(participant => participant.ChatRoomId);
            entity.HasMany(room => room.Messages)
                .WithOne(message => message.ChatRoom)
                .HasForeignKey(message => message.ChatRoomId);
        });

        modelBuilder.Entity<ChatParticipant>(entity =>
        {
            entity.ToTable("chat_participants");
            entity.HasKey(participant => participant.Id);
            entity.Property(participant => participant.ShadowUserId).IsRequired();
            entity.Property(participant => participant.JoinedAt).IsRequired();
            entity.HasIndex(participant => new { participant.ChatRoomId, participant.ShadowUserId }).IsUnique();
            entity.HasIndex(participant => participant.ShadowUserId);

            entity.HasOne(participant => participant.ShadowUser)
                .WithMany()
                .HasForeignKey(participant => participant.ShadowUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.ToTable("messages");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.SenderId).IsRequired();
            entity.Property(message => message.Type).HasConversion<string>().IsRequired();
            entity.Property(message => message.Content).HasMaxLength(4096).IsRequired();
            entity.Property(message => message.Metadata).HasColumnType("nvarchar(max)");
            entity.Property(message => message.CreatedAt).IsRequired();
            entity.HasIndex(message => new { message.ChatRoomId, message.CreatedAt });
            entity.HasIndex(message => message.SenderId);
            entity.HasMany(message => message.Statuses)
                .WithOne(status => status.Message)
                .HasForeignKey(status => status.MessageId);

            entity.HasOne(message => message.Sender)
                .WithMany()
                .HasForeignKey(message => message.SenderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MessageStatus>(entity =>
        {
            entity.ToTable("message_status");
            entity.HasKey(status => status.Id);
            entity.Property(status => status.RecipientId).IsRequired();
            entity.Property(status => status.Status).HasConversion<string>().IsRequired();
            entity.Property(status => status.UpdatedAt).IsRequired();
            entity.HasIndex(status => new { status.MessageId, status.RecipientId }).IsUnique();
            entity.HasIndex(status => status.RecipientId);
            entity.HasIndex(status => new { status.RecipientId, status.Status });
        });

        modelBuilder.Entity<UserShadow>(entity =>
        {
            entity.ToTable("users_shadow");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Email).HasMaxLength(256).IsRequired();
            entity.Property(user => user.DisplayName).HasMaxLength(256);
            entity.Property(user => user.AvatarUrl).HasMaxLength(1024);
            entity.Property(user => user.FirebaseUid).HasMaxLength(128);
            entity.Property(user => user.Role).HasMaxLength(64);
            entity.Property(user => user.Status).HasMaxLength(64);
            entity.Property(user => user.UpdatedAt).IsRequired();
            entity.HasIndex(user => user.Email);
            entity.HasIndex(user => user.FirebaseUid);
        });

        modelBuilder.Entity<EventStoreCheckpoint>(entity =>
        {
            entity.ToTable("eventstore_checkpoints");
            entity.HasKey(checkpoint => checkpoint.Name);
            entity.Property(checkpoint => checkpoint.Name).HasMaxLength(128).IsRequired();
            entity.Property(checkpoint => checkpoint.UpdatedAt).IsRequired();
        });
    }
}
