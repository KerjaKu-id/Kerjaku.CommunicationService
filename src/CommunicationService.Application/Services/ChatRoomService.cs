using CommunicationService.Application.Abstractions.Repositories;
using CommunicationService.Application.Abstractions.Time;
using CommunicationService.Application.DTOs;
using CommunicationService.Application.DTOs.Requests;
using CommunicationService.Application.Exceptions;
using CommunicationService.Application.Mapping;
using CommunicationService.Application.Options;
using CommunicationService.Domain.Entities;

namespace CommunicationService.Application.Services;

public class ChatRoomService : IChatRoomService
{
    private readonly IChatRoomRepository _chatRoomRepository;
    private readonly TemporaryChatOptions _temporaryChatOptions;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ChatRoomService(
        IChatRoomRepository chatRoomRepository,
        TemporaryChatOptions temporaryChatOptions,
        IDateTimeProvider dateTimeProvider)
    {
        _chatRoomRepository = chatRoomRepository;
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
}
