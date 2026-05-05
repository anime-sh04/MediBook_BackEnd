using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MediBook.Notification.API.Hubs;

/// <summary>
/// SignalR hub for real-time in-app notifications.
///
/// Clients connect with their JWT token; the hub places each user in a
/// group named after their UserId so the service can push directly to a
/// specific user without broadcasting to everyone.
///
/// JavaScript client example:
///   const connection = new signalR.HubConnectionBuilder()
///       .withUrl("/hubs/notifications", { accessTokenFactory: () => token })
///       .build();
///   connection.on("ReceiveNotification", (payload) => { ... });
///   await connection.start();
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects.
    /// Adds the connection to a group keyed by UserId so we can
    /// send targeted notifications via IHubContext.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
            _logger.LogInformation(
                "SignalR: User {UserId} connected (ConnectionId={ConnId})",
                userId, Context.ConnectionId);
        }
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
            _logger.LogInformation(
                "SignalR: User {UserId} disconnected (ConnectionId={ConnId})",
                userId, Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }
}
