using CommunicationService.Domain.Entities;
using CommunicationService.Domain.Enums;
using CommunicationService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CommunicationService.Tests.Integration.TestUtilities;

public sealed class GatewayDbHelper
{
    private readonly GatewayTestFixture _fixture;

    public GatewayDbHelper(GatewayTestFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task<ChatRoom?> GetChatRoomAsync(Guid roomId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.ChatRooms
            .AsNoTracking()
            .FirstOrDefaultAsync(room => room.Id == roomId);
    }

    public async Task<int> CountParticipantsAsync(Guid roomId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.ChatParticipants
            .AsNoTracking()
            .CountAsync(participant => participant.ChatRoomId == roomId);
    }

    public async Task<Message?> GetMessageAsync(Guid messageId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Messages
            .AsNoTracking()
            .FirstOrDefaultAsync(message => message.Id == messageId);
    }

    public async Task<int> CountMessagesAsync(Guid roomId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Messages
            .AsNoTracking()
            .CountAsync(message => message.ChatRoomId == roomId);
    }

    public async Task<int> CountMessageStatusesAsync(Guid messageId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.MessageStatuses
            .AsNoTracking()
            .CountAsync(status => status.MessageId == messageId);
    }

    public async Task<MessageStatus?> GetMessageStatusAsync(Guid messageId, Guid recipientId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.MessageStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(status => status.MessageId == messageId && status.RecipientId == recipientId);
    }

    public async Task<bool> IsChatRoomExpiredAsync(Guid roomId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.ChatRooms
            .AsNoTracking()
            .Where(room => room.Id == roomId)
            .Select(room => room.IsExpired)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> IsMessageStatusAsync(Guid messageId, Guid recipientId, MessageDeliveryStatus status)
    {
        var messageStatus = await GetMessageStatusAsync(messageId, recipientId);
        return messageStatus?.Status == status;
    }
}
