using CommunicationService.Application.DTOs;
using CommunicationService.Domain.Entities;

namespace CommunicationService.Application.Mapping;

public static class ChatRoomMapper
{
    public static ChatRoomDto ToDto(ChatRoom room, ChatRoomSummary? summary = null)
    {
        var status = room.HasExpired(DateTimeOffset.UtcNow) ? "expired" : room.Status switch
        {
            ChatRoomStatus.Archived => "archived",
            ChatRoomStatus.Expired => "expired",
            _ => "active"
        };

        return new ChatRoomDto
        {
            Id = room.Id,
            IsTemporary = room.IsTemporary,
            ExpiresAt = room.ExpiresAt,
            IsExpired = room.IsExpired,
            CreatedAt = room.CreatedAt,
            Participants = room.Participants.Select(p => p.ShadowUserId).ToArray(),
            RoomType = summary?.RoomType ?? FormatRoomType(room.RoomType),
            Status = summary?.Status ?? status,
            OtherPartyId = summary?.OtherPartyId,
            OtherPartyName = summary?.OtherPartyName,
            OtherPartyAvatar = summary?.OtherPartyAvatar,
            OtherPartyEmail = summary?.OtherPartyEmail,
            LastMessage = summary?.LastMessage,
            LastMessageAt = summary?.LastMessageAt,
            UnreadCount = summary?.UnreadCount ?? 0
        };
    }

    private static string FormatRoomType(ChatRoomType roomType)
        => roomType switch
        {
            ChatRoomType.CustomerService => "customer_service",
            ChatRoomType.CustomerPartner => "customer_partner",
            ChatRoomType.PartnerTeam => "partner_team",
            ChatRoomType.AdminEscalation => "admin_escalation",
            ChatRoomType.GroupChat => "group_chat",
            _ => "customer_partner"
        };
}

public sealed record ChatRoomSummary(
    string RoomType,
    string Status,
    Guid? OtherPartyId,
    string? OtherPartyName,
    string? OtherPartyAvatar,
    string? OtherPartyEmail,
    string? LastMessage,
    DateTimeOffset? LastMessageAt,
    int UnreadCount);
