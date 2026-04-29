using CommunicationService.Application.DTOs.Requests;
using CommunicationService.Application.Services;
using Microsoft.AspNetCore.SignalR;

namespace CommunicationService.Api.Hubs;

public class ChatHub : Hub
{
    private readonly IChatRoomService _chatRoomService;
    private readonly IMessageService _messageService;

    public ChatHub(IChatRoomService chatRoomService, IMessageService messageService)
    {
        _chatRoomService = chatRoomService;
        _messageService = messageService;
    }

    public async Task JoinRoom(Guid roomId, Guid userId)
    {
        var room = await _chatRoomService.GetRoomAsync(roomId, Context.ConnectionAborted);
        if (!room.Participants.Contains(userId))
        {
            throw new HubException("User is not a participant of this room.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroupName(roomId));
    }

    public async Task SendMessage(SendMessageRequest request)
    {
        var message = await _messageService.SendMessageAsync(request, Context.ConnectionAborted);
        await Clients.Group(RoomGroupName(request.RoomId))
            .SendAsync("ReceiveMessage", message, Context.ConnectionAborted);
    }

    public async Task MarkRead(Guid messageId, Guid readerId)
    {
        var status = await _messageService.MarkMessageReadAsync(messageId, readerId, Context.ConnectionAborted);
        await Clients.Group(RoomGroupName(status.RoomId))
            .SendAsync("MessageRead", status, Context.ConnectionAborted);
    }

    private static string RoomGroupName(Guid roomId)
    {
        return $"room-{roomId}";
    }
}
