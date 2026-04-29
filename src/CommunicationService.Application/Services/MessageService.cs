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

namespace CommunicationService.Application.Services;

public class MessageService : IMessageService
{
    private const int MaxPageSize = 100;

    private readonly IChatRoomRepository _chatRoomRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IMessageStatusRepository _messageStatusRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly IDateTimeProvider _dateTimeProvider;

    public MessageService(
        IChatRoomRepository chatRoomRepository,
        IMessageRepository messageRepository,
        IMessageStatusRepository messageStatusRepository,
        IEventPublisher eventPublisher,
        IDateTimeProvider dateTimeProvider)
    {
        _chatRoomRepository = chatRoomRepository;
        _messageRepository = messageRepository;
        _messageStatusRepository = messageStatusRepository;
        _eventPublisher = eventPublisher;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<MessageDto> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken)
    {
        var content = request.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ValidationException("Message content is required.");
        }

        if (request.Type == MessageType.Image && !Uri.TryCreate(content, UriKind.Absolute, out _))
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

        if (!room.Participants.Any(p => p.UserId == request.SenderId))
        {
            throw new ValidationException("Sender is not a participant of the room.");
        }

        var message = new Message(request.RoomId, request.SenderId, request.Type, content, now);
        foreach (var participant in room.Participants.Where(p => p.UserId != request.SenderId))
        {
            message.AddStatus(participant.UserId, MessageDeliveryStatus.Sent, now);
        }

        await _messageRepository.AddAsync(message, cancellationToken);
        await _messageStatusRepository.AddRangeAsync(message.Statuses, cancellationToken);
        await _messageRepository.SaveChangesAsync(cancellationToken);

        var dto = MessageMapper.ToDto(message);
        await _eventPublisher.PublishAsync(
            new MessageSentEvent(message.Id, message.ChatRoomId, message.SenderId, message.Type, message.CreatedAt),
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

        return new PagedResult<MessageDto>
        {
            Items = page.Items.Select(MessageMapper.ToDto).ToArray(),
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
}
