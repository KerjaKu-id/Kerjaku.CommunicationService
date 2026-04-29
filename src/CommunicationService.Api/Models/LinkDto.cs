namespace CommunicationService.Api.Models;

public sealed class LinkDto
{
    public string Rel { get; init; } = string.Empty;
    public string Href { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
}
