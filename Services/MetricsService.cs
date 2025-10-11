using System.Diagnostics;

namespace SiteBarnaQ.Services
{
    public interface IMetricsService
    {
        Task<ServerMetrics> GetCurrentMetricsAsync();
        void RecordConnection();
        void RecordDisconnection();
        void RecordMessageSent();
        void RecordMessageReceived();
    }

    public class MetricsService : IMetricsService
    {
        private readonly IConnectionManager _connectionManager;
        private long _messagesSent;
        private long _messagesReceived;
        private DateTime _startTime;
        private readonly ILogger<MetricsService> _logger;

        public MetricsService(
            IConnectionManager connectionManager,
            ILogger<MetricsService> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
            _startTime = DateTime.UtcNow;
        }

        public async Task<ServerMetrics> GetCurrentMetricsAsync()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                var activeStreams = await GetActiveStreamsCountAsync();

                return new ServerMetrics
                {
                    Timestamp = DateTime.UtcNow,
                    Uptime = DateTime.UtcNow - _startTime,
                    TotalConnections = _connectionManager.TotalConnections,
                    ActiveStreams = activeStreams,
                    MessagesSent = Interlocked.Read(ref _messagesSent),
                    MessagesReceived = Interlocked.Read(ref _messagesReceived),
                    MemoryUsage = process.WorkingSet64 / 1024 / 1024, // MB
                    CpuUsage = GetCpuUsage(),
                    GcCollections = new GcCollections
                    {
                        Gen0 = GC.CollectionCount(0),
                        Gen1 = GC.CollectionCount(1),
                        Gen2 = GC.CollectionCount(2)
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting metrics");
                return new ServerMetrics
                {
                    Timestamp = DateTime.UtcNow,
                    Uptime = DateTime.UtcNow - _startTime,
                    TotalConnections = _connectionManager.TotalConnections,
                    ActiveStreams = 0,
                    MessagesSent = 0,
                    MessagesReceived = 0,
                    MemoryUsage = 0,
                    CpuUsage = 0,
                    GcCollections = new GcCollections()
                };
            }
        }

        public void RecordConnection()
        {
            // برای آمار اتصالات
        }

        public void RecordDisconnection()
        {
            // برای آمار قطع اتصالات
        }

        public void RecordMessageSent()
        {
            Interlocked.Increment(ref _messagesSent);
        }

        public void RecordMessageReceived()
        {
            Interlocked.Increment(ref _messagesReceived);
        }

        private async Task<int> GetActiveStreamsCountAsync()
        {
            try
            {
                //var streams = await _streamManager.GetActiveStreams();
                return 0; //streams.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active streams count");
                return 0;
            }
        }

        private double GetCpuUsage()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                var startTime = DateTime.UtcNow;
                var startCpuUsage = process.TotalProcessorTime;

                Thread.Sleep(500);

                var endTime = DateTime.UtcNow;
                var endCpuUsage = process.TotalProcessorTime;

                var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
                var totalMsPassed = (endTime - startTime).TotalMilliseconds;
                var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);

                return cpuUsageTotal * 100;
            }
            catch
            {
                return 0;
            }
        }
    }

    public class ServerMetrics
    {
        public DateTime Timestamp { get; set; }
        public TimeSpan Uptime { get; set; }
        public int TotalConnections { get; set; }
        public int ActiveStreams { get; set; }
        public long MessagesSent { get; set; }
        public long MessagesReceived { get; set; }
        public long MemoryUsage { get; set; } // MB
        public double CpuUsage { get; set; } // Percentage
        public GcCollections GcCollections { get; set; } = new();
    }

    public class GcCollections
    {
        public int Gen0 { get; set; }
        public int Gen1 { get; set; }
        public int Gen2 { get; set; }
    }
}
