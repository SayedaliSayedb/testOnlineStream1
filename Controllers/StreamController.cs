using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WebApplication1.Hubs;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StreamController : ControllerBase
    {
        private readonly IHubContext<WebRTCHub> _hubContext;
        private readonly ILogger<StreamController> _logger;

        public StreamController(IHubContext<WebRTCHub> hubContext, ILogger<StreamController> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        [HttpGet("list")]
        public IActionResult GetStreams()
        {
            var hub = _hubContext.GetHub<WebRTCHub>();
            var streams = hub?.GetAvailableStreams() ?? new List<StreamInfo>();

            // فیلتر کردن استریم‌های فعال
            var activeStreams = streams.Where(s => s.IsLive).ToList();

            return Ok(activeStreams);
        }

        [HttpGet("stats")]
        public IActionResult GetStats()
        {
            var hub = _hubContext.GetHub<WebRTCHub>();
            var stats = hub?.GetStats() ?? new StreamStats();
            return Ok(stats);
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateStream([FromBody] CreateStreamRequest request)
        {
            try
            {
                // این فقط برای API است، در حالت عولی از WebSocket استفاده می‌شود
                return Ok(new { message = "برای ایجاد استریم از WebSocket استفاده کنید" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating stream via API");
                return BadRequest(new { error = ex.Message });
            }
        }

    }

    public class CreateStreamRequest
    {
        public string Title { get; set; } = "پخش زنده";
        public string Description { get; set; } = string.Empty;
    }
}
