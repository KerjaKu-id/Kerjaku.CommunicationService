using CommunicationService.Application.DTOs;
using CommunicationService.Application.DTOs.Requests;

namespace CommunicationService.Application.Services;

public interface IChatRoomService
{
    Task<ChatRoomDto> CreateRoomAsync(CreateChatRoomRequest request, CancellationToken cancellationToken);
    Task<ChatRoomDto> GetRoomAsync(Guid roomId, CancellationToken cancellationToken);
    Task<ChatRoomDto> GetRoomDetailsAsync(Guid roomId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ChatRoomDto>> GetRoomsForUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<ChatRoomDto> StartNegotiationAsync(Guid roomId, Guid userId, decimal price, CancellationToken cancellationToken);
    Task<ChatRoomDto> RespondToNegotiationAsync(Guid roomId, Guid userId, bool accept, CancellationToken cancellationToken);
}
