namespace CommunicationService.Api.Models;

public sealed class ApiResponse<T>
{
    public T Data { get; init; } = default!;
    public IReadOnlyCollection<LinkDto> Links { get; init; } = Array.Empty<LinkDto>();
}
