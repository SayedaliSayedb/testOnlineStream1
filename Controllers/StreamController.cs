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
            try
            {
                var hub = new WebRTCHub();
                var streams = hub.GetAvailableStreams();
                return Ok(streams);
            }
            catch (Exception ex)
            {
                return Ok(new List<StreamInfo>());
            }
        }


        [HttpGet("stats")]
        public IActionResult GetStats()
        {
            try
            {
                var hub = new WebRTCHub();
                var stats = hub.GetStats();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stats");
                return Ok(new StreamStats());
            }
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateStream([FromBody] CreateStreamRequest request)
        {
            try
            {
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
