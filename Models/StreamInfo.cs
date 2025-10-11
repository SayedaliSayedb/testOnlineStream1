namespace WebApplication1.Models
{
    public class StreamInfo
    {
        public string StreamId { get; set; } = string.Empty;
        public string StreamerConnectionId { get; set; } = string.Empty;
        public string Title { get; set; } = "پخش زنده";
        public DateTime StartTime { get; set; }
        public bool IsLive { get; set; }
        public int ViewerCount { get; set; }
        public List<ViewerInfo> Viewers { get; set; } = new();
        public string StreamerName { get; set; } = "پخش‌کننده";
    }

    public class ViewerInfo
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string Name { get; set; } = "بیننده";
        public DateTime JoinTime { get; set; }
        public string IPAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty; // اضافه شده
    }

    public class StreamStats
    {
        public int TotalViewers { get; set; }
        public int ActiveStreams { get; set; }
        public DateTime ServerStartTime { get; set; }
        public TimeSpan Uptime { get; set; }
        public int TotalUsers { get; set; }
    }
}
