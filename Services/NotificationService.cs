using Microsoft.AspNetCore.SignalR;
using WebApplication1.Hubs;
using WebApplication1.Models;
using ChatMessage = WebApplication1.Models.ChatMessage;

namespace WebApplication1.Services
{
    public interface INotificationService
    {
        Task SendNotificationToUserAsync(string connectionId, string title, string message, string type = "info");
        Task SendNotificationToAllAsync(string title, string message, string type = "info");
        Task SendNotificationToStreamAsync(string streamId, string title, string message, string type = "info");
        Task SendSystemMessageAsync(string streamId, string message);
    }
    public class NotificationService : INotificationService
    {
        private readonly IHubContext<WebRTCHub> _hubContext;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(IHubContext<WebRTCHub> hubContext, ILogger<NotificationService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task SendNotificationToUserAsync(string connectionId, string title, string message, string type = "info")
        {
            try
            {
                await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveNotification", new
                {
                    Title = title,
                    Message = message,
                    Type = type,
                    Timestamp = DateTime.UtcNow
                });

                _logger.LogDebug("Notification sent to user: {ConnectionId}, Title: {Title}", connectionId, title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to user: {ConnectionId}", connectionId);
            }
        }

        public async Task SendNotificationToAllAsync(string title, string message, string type = "info")
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("ReceiveNotification", new
                {
                    Title = title,
                    Message = message,
                    Type = type,
                    Timestamp = DateTime.UtcNow
                });

                _logger.LogInformation("Notification sent to all users: {Title}", title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to all users");
            }
        }

        public async Task SendNotificationToStreamAsync(string streamId, string title, string message, string type = "info")
        {
            try
            {
                await _hubContext.Clients.Group(streamId).SendAsync("ReceiveNotification", new
                {
                    Title = title,
                    Message = message,
                    Type = type,
                    Timestamp = DateTime.UtcNow
                });

                _logger.LogDebug("Notification sent to stream: {StreamId}, Title: {Title}", streamId, title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to stream: {StreamId}", streamId);
            }
        }

        public async Task SendSystemMessageAsync(string streamId, string message)
        {
            try
            {
                var systemMessage = new ChatMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    SenderConnectionId = "system",
                    SenderName = "سیستم",
                    SenderNationalCode = "system",
                    Message = message,
                    Timestamp = DateTime.UtcNow,
                    StreamId = streamId,
                    IsSystemMessage = true
                };

                await _hubContext.Clients.Group(streamId).SendAsync("ChatMessageReceived", systemMessage);
                _logger.LogInformation("System message sent to stream: {StreamId}, Message: {Message}", streamId, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending system message to stream: {StreamId}", streamId);
            }
        }
    }
}
