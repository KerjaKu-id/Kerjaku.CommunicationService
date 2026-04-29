using CommunicationService.Api.Models;
using CommunicationService.Application.DTOs;
using CommunicationService.Application.DTOs.Requests;
using CommunicationService.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CommunicationService.Api.Controllers;

[ApiController]
[Route("chat/rooms")]
public class ChatRoomsController : ControllerBase
{
    private readonly IChatRoomService _chatRoomService;

    public ChatRoomsController(IChatRoomService chatRoomService)
    {
        _chatRoomService = chatRoomService;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ChatRoomDto>>> Create([FromBody] CreateChatRoomRequest request, CancellationToken cancellationToken)
    {
        var room = await _chatRoomService.CreateRoomAsync(request, cancellationToken);
        var response = new ApiResponse<ChatRoomDto>
        {
            Data = room,
            Links = BuildRoomLinks(room.Id)
        };

        return CreatedAtAction(nameof(GetById), new { id = room.Id }, response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ChatRoomDto>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var room = await _chatRoomService.GetRoomAsync(id, cancellationToken);
        var response = new ApiResponse<ChatRoomDto>
        {
            Data = room,
            Links = BuildRoomLinks(id)
        };

        return Ok(response);
    }

    private IReadOnlyCollection<LinkDto> BuildRoomLinks(Guid roomId)
    {
        var self = Url.Action(nameof(GetById), "ChatRooms", new { id = roomId }) ?? $"/chat/rooms/{roomId}";
        var sendMessage = Url.Action(nameof(MessagesController.Send), "Messages") ?? "/chat/messages";
        var getMessages = Url.Action(nameof(MessagesController.Get), "Messages", new { roomId })
            ?? $"/chat/messages?roomId={roomId}";

        return new[]
        {
            new LinkDto { Rel = "self", Href = self, Method = "GET" },
            new LinkDto { Rel = "send_message", Href = sendMessage, Method = "POST" },
            new LinkDto { Rel = "get_messages", Href = getMessages, Method = "GET" }
        };
    }
}
