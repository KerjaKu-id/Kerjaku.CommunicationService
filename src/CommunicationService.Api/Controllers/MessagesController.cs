using CommunicationService.Api.Models;
using CommunicationService.Application.DTOs;
using CommunicationService.Application.DTOs.Requests;
using CommunicationService.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CommunicationService.Api.Controllers;

[ApiController]
[Route("chat/messages")]
public class MessagesController : ControllerBase
{
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 20;

    private readonly IMessageService _messageService;

    public MessagesController(IMessageService messageService)
    {
        _messageService = messageService;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<MessageDto>>> Send([FromBody] SendMessageRequest request, CancellationToken cancellationToken)
    {
        var message = await _messageService.SendMessageAsync(request, cancellationToken);
        var response = new ApiResponse<MessageDto>
        {
            Data = message,
            Links = BuildMessageLinks(request.RoomId, DefaultPageNumber, DefaultPageSize, hasNext: false)
        };

        return Ok(response);
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<MessageDto>>>> Get(
        [FromQuery] Guid roomId,
        [FromQuery] int pageNumber = DefaultPageNumber,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var messages = await _messageService.GetMessagesAsync(roomId, pageNumber, pageSize, cancellationToken);
        var response = new ApiResponse<PagedResult<MessageDto>>
        {
            Data = messages,
            Links = BuildMessageLinks(roomId, pageNumber, pageSize, messages.HasNext)
        };

        return Ok(response);
    }

    private IReadOnlyCollection<LinkDto> BuildMessageLinks(Guid roomId, int pageNumber, int pageSize, bool hasNext)
    {
        var self = Url.Action(nameof(Get), "Messages", new { roomId, pageNumber, pageSize })
            ?? $"/chat/messages?roomId={roomId}&pageNumber={pageNumber}&pageSize={pageSize}";
        var sendMessage = Url.Action(nameof(Send), "Messages") ?? "/chat/messages";
        var links = new List<LinkDto>
        {
            new LinkDto { Rel = "self", Href = self, Method = "GET" },
            new LinkDto { Rel = "send_message", Href = sendMessage, Method = "POST" },
            new LinkDto { Rel = "get_messages", Href = self, Method = "GET" }
        };

        if (hasNext)
        {
            var nextPage = Url.Action(nameof(Get), "Messages", new { roomId, pageNumber = pageNumber + 1, pageSize })
                ?? $"/chat/messages?roomId={roomId}&pageNumber={pageNumber + 1}&pageSize={pageSize}";
            links.Add(new LinkDto { Rel = "next", Href = nextPage, Method = "GET" });
        }

        return links;
    }
}
