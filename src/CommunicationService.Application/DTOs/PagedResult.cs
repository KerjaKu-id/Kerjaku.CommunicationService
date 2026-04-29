namespace CommunicationService.Application.DTOs;

public sealed class PagedResult<T>
{
    public IReadOnlyCollection<T> Items { get; init; } = Array.Empty<T>();
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public bool HasNext { get; init; }
}
