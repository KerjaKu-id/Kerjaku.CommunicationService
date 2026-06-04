using System;

namespace CommunicationService.Domain.Entities;

/// <summary>
/// Domain model representing a user notification.
/// Centralized notification system entity.
/// </summary>
public class Notification
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public string Type { get; private set; } = string.Empty; // e.g., "kyc", "order", "chat"
    public string? ReferenceId { get; private set; } // e.g., orderId, kycId
    public bool IsRead { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Notification()
    {
    }

    public Notification(Guid userId, string title, string content, string type, string? referenceId = null)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Title = title;
        Content = content;
        Type = type;
        ReferenceId = referenceId;
        IsRead = false;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkAsRead()
    {
        IsRead = true;
    }
}
