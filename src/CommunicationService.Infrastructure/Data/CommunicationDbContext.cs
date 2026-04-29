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
            entity.Property(participant => participant.UserId).IsRequired();
            entity.Property(participant => participant.JoinedAt).IsRequired();
            entity.HasIndex(participant => new { participant.ChatRoomId, participant.UserId }).IsUnique();
            entity.HasIndex(participant => participant.UserId);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.ToTable("messages");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.SenderId).IsRequired();
            entity.Property(message => message.Type).HasConversion<string>().IsRequired();
            entity.Property(message => message.Content).HasMaxLength(4096).IsRequired();
            entity.Property(message => message.CreatedAt).IsRequired();
            entity.HasIndex(message => new { message.ChatRoomId, message.CreatedAt });
            entity.HasIndex(message => message.SenderId);
            entity.HasMany(message => message.Statuses)
                .WithOne(status => status.Message)
                .HasForeignKey(status => status.MessageId);
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
    }
}
