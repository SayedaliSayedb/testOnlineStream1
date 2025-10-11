using Microsoft.AspNetCore.Mvc;
using SiteBarnaQ.Services;

namespace SiteBarnaQ.Controllers
{
    public class HealthController : ControllerBase
    {
        private readonly IConnectionManager _connectionManager;
        private readonly IMetricsService _metricsService;
        private readonly ILogger<HealthController> _logger;

        public HealthController(
            IConnectionManager connectionManager,
            IMetricsService metricsService,
            ILogger<HealthController> logger)
        {
            _connectionManager = connectionManager;
            _metricsService = metricsService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                var metrics = await _metricsService.GetCurrentMetricsAsync();

                return Ok(new
                {
                    status = "Healthy",
                    timestamp = metrics.Timestamp,
                    server = Environment.MachineName,
                    version = "1.0.0",
                    connections = metrics.TotalConnections,
                    activeStreams = metrics.ActiveStreams,
                    uptime = metrics.Uptime.ToString(@"dd\.hh\:mm\:ss")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in health check");
                return StatusCode(500, new { status = "Unhealthy", error = ex.Message });
            }
        }

        public async Task<IActionResult> GetDetailed()
        {
            try
            {
                var metrics = await _metricsService.GetCurrentMetricsAsync();

                return Ok(new
                {
                    status = "Healthy",
                    server = new
                    {
                        name = Environment.MachineName,
                        os = Environment.OSVersion.ToString(),
                        version = "1.0.0"
                    },
                    metrics = new
                    {
                        connections = new
                        {
                            total = metrics.TotalConnections,
                            active = _connectionManager.ActiveConnections.Count
                        },
                        streams = metrics.ActiveStreams,
                        messages = new
                        {
                            sent = metrics.MessagesSent,
                            received = metrics.MessagesReceived
                        },
                        performance = new
                        {
                            memory_mb = metrics.MemoryUsage,
                            cpu_percent = Math.Round(metrics.CpuUsage, 2),
                            uptime = metrics.Uptime.ToString(@"dd\.hh\:mm\:ss")
                        },
                        gc = metrics.GcCollections
                    },
                    timestamp = metrics.Timestamp
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in detailed health check");
                return StatusCode(500, new { status = "Unhealthy", error = ex.Message });
            }
        }

        [HttpGet("simple")]
        public IActionResult GetSimple()
        {
            return Ok(new
            {
                status = "Healthy",
                timestamp = DateTime.UtcNow,
                connections = _connectionManager.TotalConnections
            });
        }
    }
}
