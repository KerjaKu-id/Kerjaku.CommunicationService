namespace CommunicationService.Application.Exceptions;

public class ResourceExpiredException : Exception
{
    public ResourceExpiredException(string message) : base(message)
    {
    }
}
