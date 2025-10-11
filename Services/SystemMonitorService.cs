using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;
using WebApplication1.Hubs;
using WebApplication1.Models;
using WebApplication1.Services.Interfaces;

namespace WebApplication1.Services
{
    public class SystemMonitorService : ISystemMonitorService
    {
        private readonly IHubContext<WebRTCHub> _hubContext;
        private readonly ILogger<SystemMonitorService> _logger;
        private DateTime _lastCpuTime;
        private TimeSpan _lastCpuUsage;
        private readonly List<SystemResources> _resourceHistory = new();

        public SystemMonitorService(IHubContext<WebRTCHub> hubContext, ILogger<SystemMonitorService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
            _lastCpuTime = DateTime.UtcNow;
            _lastCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
        }

        public async Task<SystemResources> GetSystemResourcesAsync()
        {
            try
            {
                var process = Process.GetCurrentProcess();

                // محاسبه CPU usage
                var currentTime = DateTime.UtcNow;
                var currentCpuUsage = process.TotalProcessorTime;
                var cpuUsedMs = (currentCpuUsage - _lastCpuUsage).TotalMilliseconds;
                var totalMsPassed = (currentTime - _lastCpuTime).TotalMilliseconds;
                var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);

                _lastCpuTime = currentTime;
                _lastCpuUsage = currentCpuUsage;

                var hub = GetHub();
                var stats = hub?.GetStats() ?? new StreamStats();

                var resources = new SystemResources
                {
                    CpuUsage = Math.Round(cpuUsageTotal * 100, 2),
                    MemoryUsage = process.WorkingSet64 / 1024 / 1024, // MB
                    PrivateMemory = process.PrivateMemorySize64 / 1024 / 1024, // MB
                    ActiveConnections = stats.TotalViewers,
                    ActiveStreams = stats.ActiveStreams,
                    TotalUsers = stats.TotalUsers,
                    MonitorTime = DateTime.UtcNow,
                    ServerVersion = "1.0.0"
                };

                // ذخیره تاریخچه
                _resourceHistory.Add(resources);

                // حفظ فقط 100 رکورد آخر
                if (_resourceHistory.Count > 100)
                    _resourceHistory.RemoveAt(0);

                return resources;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system resources");
                return new SystemResources();
            }
        }

        public Task<StreamStats> GetStreamStatsAsync()
        {
            var hub = GetHub();
            return Task.FromResult(hub?.GetStats() ?? new StreamStats());
        }

        public Task<List<SystemResources>> GetResourceHistoryAsync(TimeSpan duration)
        {
            var cutoffTime = DateTime.UtcNow - duration;
            var history = _resourceHistory
                .Where(r => r.MonitorTime >= cutoffTime)
                .ToList();

            return Task.FromResult(history);
        }

        private WebRTCHub GetHub()
        {
            // این متد نیاز به دسترسی به instance هاب دارد
            // در عمل بهتر است از dependency injection استفاده شود
            return null;
        }
    }
}
