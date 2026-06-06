using CommunicationService.Application.Abstractions.Events;
using CommunicationService.Application.Abstractions.Repositories;
using CommunicationService.Application.Abstractions.Time;
using CommunicationService.Application.DTOs;
using CommunicationService.Application.DTOs.Requests;
using CommunicationService.Application.Events;
using CommunicationService.Application.Exceptions;
using CommunicationService.Application.Mapping;
using CommunicationService.Domain.Entities;
using CommunicationService.Domain.Enums;
using System.Text.Json;

namespace CommunicationService.Application.Services;

public class MessageService : IMessageService
{
    private const int MaxPageSize = 100;

    private readonly IChatRoomRepository _chatRoomRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IMessageStatusRepository _messageStatusRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUserShadowRepository _userShadowRepository;

    public MessageService(
        IChatRoomRepository chatRoomRepository,
        IMessageRepository messageRepository,
        IMessageStatusRepository messageStatusRepository,
        IEventPublisher eventPublisher,
        IDateTimeProvider dateTimeProvider,
        IUserShadowRepository userShadowRepository)
    {
        _chatRoomRepository = chatRoomRepository;
        _messageRepository = messageRepository;
        _messageStatusRepository = messageStatusRepository;
        _eventPublisher = eventPublisher;
        _dateTimeProvider = dateTimeProvider;
        _userShadowRepository = userShadowRepository;
    }

    public async Task<MessageDto> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken)
    {
        var content = request.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ValidationException("Message content is required.");
        }

        var resolvedType = MessageTypeMapper.FromApiValue(request.MessageType, request.Type);

        if (resolvedType == MessageType.Image && !Uri.TryCreate(content, UriKind.Absolute, out _))
        {
            throw new ValidationException("Image messages must use a valid URL.");
        }

        var room = await _chatRoomRepository.GetByIdAsync(request.RoomId, cancellationToken);
        if (room == null)
        {
            throw new NotFoundException("Chat room not found.");
        }

        var now = _dateTimeProvider.UtcNow;
        if (room.HasExpired(now))
        {
            throw new ResourceExpiredException("Chat room has expired.");
        }

        if (!room.Participants.Any(p => p.ShadowUserId == request.SenderId))
        {
            throw new ValidationException("Sender is not a participant of the room.");
        }

        var recipientIds = room.Participants
            .Where(p => p.ShadowUserId != request.SenderId)
            .Select(p => p.ShadowUserId)
            .ToArray();

        var metadataJson = request.Metadata is null
            ? null
            : JsonSerializer.Serialize(request.Metadata, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var message = new Message(request.RoomId, request.SenderId, resolvedType, content, now, metadataJson);
        foreach (var recipientId in recipientIds)
        {
            message.AddStatus(recipientId, MessageDeliveryStatus.Sent, now);
        }

        await _messageRepository.AddAsync(message, cancellationToken);
        await _messageStatusRepository.AddRangeAsync(message.Statuses, cancellationToken);
        await _messageRepository.SaveChangesAsync(cancellationToken);

        var sender = await _userShadowRepository.GetByIdAsync(message.SenderId, cancellationToken);
        var dto = MessageMapper.ToDto(
            message,
            MessageTypeMapper.ToApiValue(resolvedType),
            DeserializeMetadata(metadataJson),
            sender?.FormattedName ?? sender?.Email,
            sender?.AvatarUrl);
        await _eventPublisher.PublishAsync(
            new MessageSentEvent(
                message.Id,
                message.ChatRoomId,
                message.SenderId,
                resolvedType,
                message.CreatedAt,
                content,
                recipientIds),
            cancellationToken);

        return dto;
    }

    public async Task<PagedResult<MessageDto>> GetMessagesAsync(
        Guid roomId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (pageNumber < 1)
        {
            throw new ValidationException("pageNumber must be greater than 0.");
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            throw new ValidationException($"pageSize must be between 1 and {MaxPageSize}.");
        }

        var room = await _chatRoomRepository.GetByIdAsync(roomId, cancellationToken);
        if (room == null)
        {
            throw new NotFoundException("Chat room not found.");
        }

        if (room.HasExpired(_dateTimeProvider.UtcNow))
        {
            throw new ResourceExpiredException("Chat room has expired.");
        }

        var page = await _messageRepository.GetByRoomIdPagedAsync(roomId, pageNumber, pageSize, cancellationToken);
        var senderIds = page.Items.Select(item => item.SenderId).Distinct().ToArray();
        var senderMap = await _userShadowRepository.GetByIdsAsync(senderIds, cancellationToken);

        return new PagedResult<MessageDto>
        {
            Items = page.Items.Select(message =>
            {
                senderMap.TryGetValue(message.SenderId, out var sender);
                return MessageMapper.ToDto(
                    message,
                    MessageTypeMapper.ToApiValue(message.Type),
                    DeserializeMetadata(message.Metadata),
                    sender?.FormattedName ?? sender?.Email,
                    sender?.AvatarUrl);
            }).ToArray(),
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            HasNext = page.HasNext
        };
    }

    public async Task<MessageStatusDto> MarkMessageReadAsync(Guid messageId, Guid readerId, CancellationToken cancellationToken)
    {
        var message = await _messageRepository.GetByIdAsync(messageId, cancellationToken);
        if (message == null)
        {
            throw new NotFoundException("Message not found.");
        }

        var room = await _chatRoomRepository.GetByIdAsync(message.ChatRoomId, cancellationToken);
        if (room == null)
        {
            throw new NotFoundException("Chat room not found.");
        }

        if (room.HasExpired(_dateTimeProvider.UtcNow))
        {
            throw new ResourceExpiredException("Chat room has expired.");
        }

        var now = _dateTimeProvider.UtcNow;
        var status = await _messageStatusRepository.GetAsync(messageId, readerId, cancellationToken);
        if (status == null)
        {
            status = new MessageStatus(messageId, readerId, MessageDeliveryStatus.Read, now);
            await _messageStatusRepository.AddAsync(status, cancellationToken);
        }
        else
        {
            status.UpdateStatus(MessageDeliveryStatus.Read, now);
        }

        await _messageStatusRepository.SaveChangesAsync(cancellationToken);

        var dto = MessageStatusMapper.ToDto(status, message.ChatRoomId);

        await _eventPublisher.PublishAsync(
            new MessageReadEvent(messageId, message.ChatRoomId, readerId, status.UpdatedAt),
            cancellationToken);

        return dto;
    }

    public async Task MarkRoomMessagesAsReadAsync(Guid roomId, Guid recipientId, CancellationToken cancellationToken)
    {
        // ─── BULK READ RECEIPT TRIGGER ────────────────────────────────────────
        // Mark all messages in the room as read for this user, then publish read event.
        var room = await _chatRoomRepository.GetByIdAsync(roomId, cancellationToken);
        if (room == null)
        {
            throw new NotFoundException("Chat room not found.");
        }

        var now = _dateTimeProvider.UtcNow;
        await _messageStatusRepository.MarkRoomMessagesAsReadAsync(roomId, recipientId, now, cancellationToken);
        await _messageStatusRepository.SaveChangesAsync(cancellationToken);

        await _eventPublisher.PublishAsync(
            new MessageReadEvent(Guid.Empty, roomId, recipientId, now),
            cancellationToken);
    }

    public async Task UpdateInvoiceStatusAsync(Guid invoiceId, string status, CancellationToken cancellationToken)
    {
        // ─── SYNC INVOICE STATUS METADATA ─────────────────────────────────────
        // Fetch invoice message, deserialize metadata JSON, update status, and save.
        var message = await _messageRepository.GetByInvoiceIdAsync(invoiceId, cancellationToken);
        if (message == null)
        {
            throw new NotFoundException($"Message for Invoice {invoiceId} not found.");
        }

        var metadataDict = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(message.Metadata))
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, object>>(message.Metadata);
            if (parsed != null)
            {
                metadataDict = parsed;
            }
        }

        metadataDict["status"] = status;

        var updatedMetadata = JsonSerializer.Serialize(metadataDict, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        message.UpdateMetadata(updatedMetadata);

        await _messageRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<MessageDto?> GetLatestMessageInRoomAsync(Guid roomId, CancellationToken cancellationToken)
    {
        var message = await _messageRepository.GetLatestByRoomIdAsync(roomId, cancellationToken);
        if (message == null) return null;

        var sender = await _userShadowRepository.GetByIdAsync(message.SenderId, cancellationToken);
        return MessageMapper.ToDto(
            message,
            MessageTypeMapper.ToApiValue(message.Type),
            DeserializeMetadata(message.Metadata),
            sender?.FormattedName ?? sender?.Email,
            sender?.AvatarUrl);
    }

    private static object? DeserializeMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<object>(metadataJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch
        {
            return null;
        }
    }
}
