namespace WebApplication1.Models
{
    public class ChatMessage
    {
        public string Id { get; set; } = string.Empty;
        public string SenderConnectionId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string SenderNationalCode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string StreamId { get; set; } = string.Empty;
        public bool IsSystemMessage { get; set; }
    }
    public class HeartEvent
    {
        public string Id { get; set; } = string.Empty;
        public string SenderConnectionId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string StreamId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
