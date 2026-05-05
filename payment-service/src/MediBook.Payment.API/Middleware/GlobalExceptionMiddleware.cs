using System.Net;
using System.Text.Json;
using MediBook.Payment.API.DTOs;

namespace MediBook.Payment.API.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate                    _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ArgumentException ex)
        {
            await HandleAsync(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await HandleAsync(context, HttpStatusCode.Conflict, ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            await HandleAsync(context, HttpStatusCode.NotFound, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in payment-service.");
            await HandleAsync(context, HttpStatusCode.InternalServerError,
                "An unexpected error occurred. Please try again later.");
        }
    }

    private static Task HandleAsync(HttpContext context, HttpStatusCode code, string message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode  = (int)code;

        var json = JsonSerializer.Serialize(
            new ApiErrorResponse(message),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        return context.Response.WriteAsync(json);
    }
}
