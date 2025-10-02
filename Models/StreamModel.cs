namespace WebApplication1.Models
{
    public class StreamInfo
    {
        public string StreamId { get; set; } = string.Empty;
        public string StreamerConnectionId { get; set; } = string.Empty;
        public string Title { get; set; } = "پخش زنده";
        public DateTime StartTime { get; set; }
        public int ViewerCount { get; set; }
        public bool IsLive { get; set; }
        public List<ViewerInfo> Viewers { get; set; } = new();
    }

    public class ViewerInfo
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string Name { get; set; } = "بیننده";
        public DateTime JoinTime { get; set; }
        public string IPAddress { get; set; } = string.Empty;
    }

    public class WebRTCSignal
    {
        public string TargetConnectionId { get; set; } = string.Empty;
        public string SenderConnectionId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // offer, answer, candidate
        public string Data { get; set; } = string.Empty;
    }

    public class StreamStats
    {
        public int TotalViewers { get; set; }
        public int ActiveStreams { get; set; }
        public DateTime ServerStartTime { get; set; } = DateTime.UtcNow;
        public TimeSpan Uptime => DateTime.UtcNow - ServerStartTime;
    }

    public class HomeViewModel
    {
        public string Title { get; set; } = "سیستم پخش زنده";
        public string Description { get; set; } = "پخش زنده با کیفیت بالا و تأخیر کم";
        public string Version { get; set; } = "1.0";
        public int CurrentViewers { get; set; }
        public string StreamStatus { get; set; } = "offline";
        public TimeSpan Uptime { get; set; }
    }
}
