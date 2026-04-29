using CommunicationService.Application.DTOs;
using CommunicationService.Application.DTOs.Requests;

namespace CommunicationService.Application.Services;

public interface IChatRoomService
{
    Task<ChatRoomDto> CreateRoomAsync(CreateChatRoomRequest request, CancellationToken cancellationToken);
    Task<ChatRoomDto> GetRoomAsync(Guid roomId, CancellationToken cancellationToken);
}
