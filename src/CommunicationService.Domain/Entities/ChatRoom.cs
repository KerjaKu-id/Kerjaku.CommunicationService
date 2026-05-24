using CommunicationService.Domain.Enums;

namespace CommunicationService.Domain.Entities;

/// <summary>
/// Chat Room: Container for conversations.
/// 
/// Enhanced for event-driven architecture:
/// - RoomType: Supports CustomerService, JobBased, TeamInternal, AdminEscalation
/// - OrderId: Links job-based chats to orders (nullable)
/// - Status: Tracks room lifecycle (Active, Archived, Expired)
/// 
/// Idempotent room creation via event handlers.
/// </summary>
public class ChatRoom
{
    private readonly List<ChatParticipant> _participants = new();
    private readonly List<Message> _messages = new();

    private ChatRoom()
    {
    }

    /// <summary>
    /// Legacy constructor - maintained for backward compatibility
    /// </summary>
    public ChatRoom(bool isTemporary, DateTimeOffset? expiresAt, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        IsTemporary = isTemporary;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
        IsExpired = false;
        RoomType = ChatRoomType.CustomerPartner;
        Status = ChatRoomStatus.Active;
        IsNegotiationActive = false;
        NegotiationStatus = NegotiationStatus.None;
    }

    /// <summary>
    /// New constructor for event-driven room creation
    /// </summary>
    public ChatRoom(
        ChatRoomType roomType,
        Guid? orderId,
        DateTimeOffset? expiresAt,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        RoomType = roomType;
        OrderId = orderId;
        IsTemporary = roomType == ChatRoomType.CustomerService;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
        IsExpired = false;
        Status = ChatRoomStatus.Active;
        IsNegotiationActive = false;
        NegotiationStatus = NegotiationStatus.None;
    }

    public Guid Id { get; private set; }
    public bool IsTemporary { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public bool IsExpired { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    // Event-driven properties
    public ChatRoomType RoomType { get; private set; }
    public Guid? OrderId { get; private set; }
    public ChatRoomStatus Status { get; private set; }

    // Negotiation state
    public bool IsNegotiationActive { get; private set; }
    public NegotiationStatus NegotiationStatus { get; private set; }
    public decimal? AgreedPrice { get; private set; }

    public IReadOnlyCollection<ChatParticipant> Participants => _participants;
    public IReadOnlyCollection<Message> Messages => _messages;

    public void AddParticipant(Guid userId, DateTimeOffset joinedAt)
    {
        if (_participants.Any(p => p.ShadowUserId == userId))
        {
            return;
        }

        _participants.Add(new ChatParticipant(Id, userId, joinedAt));
    }

    public void MarkExpired()
    {
        IsExpired = true;
        Status = ChatRoomStatus.Expired;
    }

    public void Archive()
    {
        Status = ChatRoomStatus.Archived;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool HasExpired(DateTimeOffset now)
    {
        return IsExpired || (ExpiresAt.HasValue && ExpiresAt.Value <= now);
    }

    // Negotiation methods
    public void StartNegotiation()
    {
        IsNegotiationActive = true;
        NegotiationStatus = NegotiationStatus.Pending;
    }

    public void AcceptNegotiation(decimal price)
    {
        IsNegotiationActive = false;
        NegotiationStatus = NegotiationStatus.Accepted;
        AgreedPrice = price;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RejectNegotiation()
    {
        IsNegotiationActive = false; // Customer can send another offer
        NegotiationStatus = NegotiationStatus.Rejected;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void CancelNegotiation()
    {
        IsNegotiationActive = false;
        NegotiationStatus = NegotiationStatus.Cancelled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public enum ChatRoomType
{
    /// <summary>Default support room - auto-created for every user</summary>
    CustomerService = 0,

    /// <summary>Job-based chat - customer + partner</summary>
    CustomerPartner = 1,

    /// <summary>Team internal - for team coordination</summary>
    PartnerTeam = 2,

    /// <summary>Admin escalation - for disputes</summary>
    AdminEscalation = 3,

    /// <summary>Group chat - multiple participants</summary>
    GroupChat = 4,
}

public enum ChatRoomStatus
{
    /// <summary>Active conversation</summary>
    Active = 0,

    /// <summary>Archived by user</summary>
    Archived = 1,

    /// <summary>Auto-closed after expiration</summary>
    Expired = 2,
}
