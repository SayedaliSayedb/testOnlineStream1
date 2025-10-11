namespace WebApplication1.Models
{
    public class SystemResources
    {
        public double CpuUsage { get; set; }
        public long MemoryUsage { get; set; }
        public long PrivateMemory { get; set; }
        public int ActiveConnections { get; set; }
        public int ActiveStreams { get; set; }
        public int TotalUsers { get; set; }
        public DateTime MonitorTime { get; set; } = DateTime.UtcNow;
        public string ServerVersion { get; set; } = "1.0.0";
    }

    public class AdminStats
    {
        public int OnlineUsers { get; set; }
        public int ActiveStreams { get; set; }
        public int TotalViewers { get; set; }
        public TimeSpan ServerUptime { get; set; }
        public SystemResources SystemResources { get; set; } = new();
    }
}
