using Microsoft.AspNetCore.Mvc;
using WebApplication1.Services.Interfaces;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SystemController : ControllerBase
    {
        private readonly ISystemMonitorService _monitorService;
        private readonly ILogger<SystemController> _logger;

        public SystemController(ISystemMonitorService monitorService, ILogger<SystemController> logger)
        {
            _monitorService = monitorService;
            _logger = logger;
        }

        [HttpGet("resources")]
        public async Task<IActionResult> GetSystemResources()
        {
            try
            {
                var resources = await _monitorService.GetSystemResourcesAsync();
                return Ok(new { success = true, data = resources });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system resources");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpGet("resources/history")]
        public async Task<IActionResult> GetResourceHistory([FromQuery] int minutes = 60)
        {
            try
            {
                var duration = TimeSpan.FromMinutes(minutes);
                var history = await _monitorService.GetResourceHistoryAsync(duration);
                return Ok(new { success = true, data = history });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting resource history");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpGet("health")]
        public async Task<IActionResult> HealthCheck()
        {
            try
            {
                var resources = await _monitorService.GetSystemResourcesAsync();

                var healthStatus = new
                {
                    status = "healthy",
                    timestamp = DateTime.UtcNow,
                    resources = new
                    {
                        resources.CpuUsage,
                        resources.MemoryUsage,
                        resources.ActiveConnections,
                        resources.ActiveStreams
                    }
                };

                return Ok(healthStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return StatusCode(503, new { status = "unhealthy", error = ex.Message });
            }
        }
    }
}
