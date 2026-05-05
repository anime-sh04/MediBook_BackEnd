using System.Text.Json;
using MediBook.Notification.API.DTOs;

namespace MediBook.Notification.API.Middleware;

/// <summary>
/// Catches any unhandled exception and returns a clean JSON error response.
/// Prevents stack traces leaking to the client in production.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate                          _next;
    private readonly ILogger<GlobalExceptionMiddleware>      _logger;
    private readonly IHostEnvironment                        _env;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GlobalExceptionMiddleware(
        RequestDelegate                         next,
        ILogger<GlobalExceptionMiddleware>      logger,
        IHostEnvironment                        env)
    {
        _next   = next;
        _logger = logger;
        _env    = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled exception on {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode  = StatusCodes.Status500InternalServerError;

            string message = _env.IsDevelopment()
                ? ex.Message
                : "An unexpected error occurred. Please try again later.";

            var error = new ApiErrorResponse(message);
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(error, _jsonOptions));
        }
    }
}
