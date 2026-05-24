using CommunicationService.Application.Abstractions.Repositories;
using CommunicationService.Application.Abstractions.Time;
using CommunicationService.Application.DTOs;
using CommunicationService.Application.DTOs.Requests;
using CommunicationService.Application.Exceptions;
using CommunicationService.Application.Mapping;
using CommunicationService.Application.Options;
using CommunicationService.Domain.Entities;
using CommunicationService.Domain.Enums;
using System.Text.Json;

namespace CommunicationService.Application.Services;

public class ChatRoomService : IChatRoomService
{
    private readonly IChatRoomRepository _chatRoomRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IMessageStatusRepository _messageStatusRepository;
    private readonly IUserShadowRepository _userShadowRepository;
    private readonly TemporaryChatOptions _temporaryChatOptions;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ChatRoomService(
        IChatRoomRepository chatRoomRepository,
        IMessageRepository messageRepository,
        IMessageStatusRepository messageStatusRepository,
        IUserShadowRepository userShadowRepository,
        TemporaryChatOptions temporaryChatOptions,
        IDateTimeProvider dateTimeProvider)
    {
        _chatRoomRepository = chatRoomRepository;
        _messageRepository = messageRepository;
        _messageStatusRepository = messageStatusRepository;
        _userShadowRepository = userShadowRepository;
        _temporaryChatOptions = temporaryChatOptions;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<ChatRoomDto> CreateRoomAsync(CreateChatRoomRequest request, CancellationToken cancellationToken)
    {
        if (request.ParticipantIds.Count < 2)
        {
            throw new ValidationException("A chat room requires at least two participants.");
        }

        var participants = request.ParticipantIds.Distinct().ToArray();
        if (participants.Length < 2)
        {
            throw new ValidationException("Participant list must contain at least two unique users.");
        }

        var now = _dateTimeProvider.UtcNow;
        DateTimeOffset? expiresAt = null;
        if (request.IsTemporary)
        {
            expiresAt = ResolveExpiration(request, now);
        }

        var room = new ChatRoom(request.IsTemporary, expiresAt, now);
        foreach (var participantId in participants)
        {
            room.AddParticipant(participantId, now);
        }

        await _chatRoomRepository.AddAsync(room, cancellationToken);
        await _chatRoomRepository.SaveChangesAsync(cancellationToken);

        return ChatRoomMapper.ToDto(room);
    }

    public async Task<ChatRoomDto> GetRoomAsync(Guid roomId, CancellationToken cancellationToken)
    {
        var room = await _chatRoomRepository.GetByIdAsync(roomId, cancellationToken);
        if (room == null)
        {
            throw new NotFoundException("Chat room not found.");
        }

        if (room.HasExpired(_dateTimeProvider.UtcNow))
        {
            throw new ResourceExpiredException("Chat room has expired.");
        }

        return ChatRoomMapper.ToDto(room);
    }

    public async Task<ChatRoomDto> GetRoomDetailsAsync(Guid roomId, Guid userId, CancellationToken cancellationToken)
    {
        var room = await _chatRoomRepository.GetByIdAsync(roomId, cancellationToken);
        if (room == null)
        {
            throw new NotFoundException("Chat room not found.");
        }

        if (room.HasExpired(_dateTimeProvider.UtcNow))
        {
            throw new ResourceExpiredException("Chat room has expired.");
        }

        var summary = await BuildSummaryAsync(room, userId, cancellationToken);
        return ChatRoomMapper.ToDto(room, summary);
    }

    public async Task<IReadOnlyCollection<ChatRoomDto>> GetRoomsForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rooms = await _chatRoomRepository.GetByParticipantAsync(userId, cancellationToken);
        if (rooms.Count == 0)
        {
            return Array.Empty<ChatRoomDto>();
        }

        var results = new List<ChatRoomDto>(rooms.Count);
        foreach (var room in rooms)
        {
            var summary = await BuildSummaryAsync(room, userId, cancellationToken);
            results.Add(ChatRoomMapper.ToDto(room, summary));
        }

        return results;
    }

    private DateTimeOffset ResolveExpiration(CreateChatRoomRequest request, DateTimeOffset now)
    {
        DateTimeOffset expiresAt;
        if (request.ExpiresAt.HasValue)
        {
            expiresAt = request.ExpiresAt.Value;
        }
        else if (request.TimeToLiveHours.HasValue)
        {
            expiresAt = now.AddHours(request.TimeToLiveHours.Value);
        }
        else
        {
            expiresAt = now.AddHours(_temporaryChatOptions.DefaultTtlHours);
        }

        if (expiresAt <= now)
        {
            throw new ValidationException("Expiration must be in the future.");
        }

        return expiresAt;
    }

    private async Task<ChatRoomSummary> BuildSummaryAsync(ChatRoom room, Guid viewerId, CancellationToken cancellationToken)
    {
        var otherPartyId = room.Participants
            .Select(p => p.ShadowUserId)
            .FirstOrDefault(id => id != viewerId);

        UserShadow? otherParty = null;
        if (otherPartyId != Guid.Empty)
        {
            var userMap = await _userShadowRepository.GetByIdsAsync(
                new[] { otherPartyId },
                cancellationToken);

            userMap.TryGetValue(otherPartyId, out otherParty);
        }

        var lastMessage = await _messageRepository.GetLatestByRoomIdAsync(room.Id, cancellationToken);
        var unreadCount = await _messageStatusRepository.CountUnreadByRoomAsync(room.Id, viewerId, cancellationToken);

        var status = room.Status switch
        {
            ChatRoomStatus.Archived => "archived",
            ChatRoomStatus.Expired => "expired",
            _ => room.HasExpired(_dateTimeProvider.UtcNow) ? "expired" : "active"
        };

        return new ChatRoomSummary(
            RoomType: room.RoomType switch
            {
                ChatRoomType.CustomerService => "customer_service",
                ChatRoomType.CustomerPartner => "customer_partner",
                ChatRoomType.PartnerTeam => "partner_team",
                ChatRoomType.AdminEscalation => "admin_escalation",
                ChatRoomType.GroupChat => "group_chat",
                _ => "customer_partner"
            },
            Status: status,
            OtherPartyId: otherParty?.Id,
            OtherPartyName: otherParty?.DisplayName ?? otherParty?.Email,
            OtherPartyAvatar: otherParty?.AvatarUrl,
            OtherPartyEmail: otherParty?.Email,
            LastMessage: lastMessage?.Content,
            LastMessageAt: lastMessage?.CreatedAt,
            UnreadCount: unreadCount);
    }

    public async Task<ChatRoomDto> StartNegotiationAsync(Guid roomId, Guid userId, decimal price, CancellationToken cancellationToken)
    {
        var room = await _chatRoomRepository.GetByIdAsync(roomId, cancellationToken);
        if (room == null) throw new NotFoundException("Chat room not found.");
        
        if (!room.Participants.Any(p => p.ShadowUserId == userId))
            throw new ValidationException("User is not a participant.");

        room.StartNegotiation();
        await _chatRoomRepository.SaveChangesAsync(cancellationToken);

        // Auto-inject offer message
        var metadata = JsonSerializer.Serialize(new { offeredPrice = price, status = "pending" });
        var message = new Message(roomId, userId, MessageType.NegotiationOffer, $"Offer: {price}", _dateTimeProvider.UtcNow, metadata);
        await _messageRepository.AddAsync(message, cancellationToken);
        await _messageRepository.SaveChangesAsync(cancellationToken);

        var summary = await BuildSummaryAsync(room, userId, cancellationToken);
        return ChatRoomMapper.ToDto(room, summary);
    }

    public async Task<ChatRoomDto> RespondToNegotiationAsync(Guid roomId, Guid userId, bool accept, CancellationToken cancellationToken)
    {
        var room = await _chatRoomRepository.GetByIdAsync(roomId, cancellationToken);
        if (room == null) throw new NotFoundException("Chat room not found.");

        if (!room.Participants.Any(p => p.ShadowUserId == userId))
            throw new ValidationException("User is not a participant.");

        if (!room.IsNegotiationActive)
            throw new ValidationException("No active negotiation.");

        // Find the latest pending offer message
        var lastMessage = await _messageRepository.GetLatestByRoomIdAsync(roomId, cancellationToken);
        decimal priceToAccept = 0;

        if (lastMessage?.Type == MessageType.NegotiationOffer && lastMessage.Metadata != null)
        {
            var metaDict = JsonSerializer.Deserialize<Dictionary<string, object>>(lastMessage.Metadata);
            if (metaDict != null && metaDict.TryGetValue("offeredPrice", out var priceObj))
            {
                if (priceObj is JsonElement el && el.TryGetDecimal(out var parsedPrice))
                {
                    priceToAccept = parsedPrice;
                }
            }
        }

        if (accept)
        {
            if (priceToAccept <= 0) throw new ValidationException("Could not determine price to accept.");
            room.AcceptNegotiation(priceToAccept);
        }
        else
        {
            room.RejectNegotiation();
        }

        await _chatRoomRepository.SaveChangesAsync(cancellationToken);

        // Add a system message about the response
        var content = accept ? $"Penawaran disetujui: Rp {priceToAccept:N0}" : "Penawaran ditolak.";
        var message = new Message(roomId, userId, MessageType.System, content, _dateTimeProvider.UtcNow);
        await _messageRepository.AddAsync(message, cancellationToken);
        await _messageRepository.SaveChangesAsync(cancellationToken);

        var summary = await BuildSummaryAsync(room, userId, cancellationToken);
        return ChatRoomMapper.ToDto(room, summary);
    }
}
