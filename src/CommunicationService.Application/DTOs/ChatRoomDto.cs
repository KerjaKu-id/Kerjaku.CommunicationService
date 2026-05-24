namespace CommunicationService.Application.DTOs;

public sealed class ChatRoomDto
{
    public Guid Id { get; init; }
    public bool IsTemporary { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public bool IsExpired { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public IReadOnlyCollection<Guid> Participants { get; init; } = Array.Empty<Guid>();
    public string RoomType { get; init; } = "customer_partner";
    public string Status { get; init; } = "active";
    
    // Negotiation state
    public bool IsNegotiationActive { get; init; }
    public string NegotiationStatus { get; init; } = "none";
    public decimal? AgreedPrice { get; init; }

    public Guid? OtherPartyId { get; init; }
    public string? OtherPartyName { get; init; }
    public string? OtherPartyAvatar { get; init; }
    public string? OtherPartyEmail { get; init; }
    public string? LastMessage { get; init; }
    public DateTimeOffset? LastMessageAt { get; init; }
    public int UnreadCount { get; init; }
}
