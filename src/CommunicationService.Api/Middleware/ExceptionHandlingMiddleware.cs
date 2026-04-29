using CommunicationService.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CommunicationService.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            await WriteProblemDetailsAsync(context, StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (NotFoundException ex)
        {
            await WriteProblemDetailsAsync(context, StatusCodes.Status404NotFound, ex.Message);
        }
        catch (ResourceExpiredException ex)
        {
            await WriteProblemDetailsAsync(context, StatusCodes.Status410Gone, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception.");
            await WriteProblemDetailsAsync(context, StatusCodes.Status500InternalServerError, "Unexpected error.");
        }
    }

    private static Task WriteProblemDetailsAsync(HttpContext context, int statusCode, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = "Request failed",
            Detail = detail
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        return context.Response.WriteAsJsonAsync(problem);
    }
}
