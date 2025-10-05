using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WebApplication1.Hubs;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ViewerController : ControllerBase
    {
        private readonly IHubContext<StreamHub> _hubContext;
        private readonly ILogger<ViewerController> _logger;
        private static readonly List<ViewerInfo> _viewers = new();

        public ViewerController(IHubContext<StreamHub> hubContext, ILogger<ViewerController> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        [HttpPost("join")]
        public IActionResult JoinStream([FromBody] ViewerJoinRequest request)
        {
            try
            {
                var viewer = new ViewerInfo
                {
                    ConnectionId = Guid.NewGuid().ToString(),
                    Name = request.Name ?? "بیننده ناشناس",
                    JoinTime = DateTime.UtcNow,
                    IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
                };

                _viewers.Add(viewer);

                _logger.LogInformation("Viewer joined: {ViewerName} from {IP}", viewer.Name, viewer.IPAddress);

                return Ok(new
                {
                    Success = true,
                    ViewerId = viewer.ConnectionId,
                    Message = "خوش آمدید! شما به پخش زنده پیوستید."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining viewer to stream");
                return BadRequest(new { Success = false, Message = "خطا در پیوستن به پخش زنده" });
            }
        }

        [HttpPost("leave")]
        public IActionResult LeaveStream([FromBody] ViewerLeaveRequest request)
        {
            try
            {
                var viewer = _viewers.FirstOrDefault(v => v.ConnectionId == request.ViewerId);
                if (viewer != null)
                {
                    _viewers.Remove(viewer);
                    _logger.LogInformation("Viewer left: {ViewerName}", viewer.Name);
                }

                return Ok(new { Success = true, Message = "با موفقیت از پخش خارج شدید" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing viewer from stream");
                return BadRequest(new { Success = false, Message = "خطا در خروج از پخش زنده" });
            }
        }

        [HttpGet("list")]
        public IActionResult GetViewers()
        {
            var viewers = _viewers.Select(v => new
            {
                v.ConnectionId,
                v.Name,
                v.JoinTime,
                WatchTime = DateTime.UtcNow - v.JoinTime
            });

            return Ok(new
            {
                TotalViewers = _viewers.Count,
                Viewers = viewers
            });
        }

        [HttpPost("chat/send")]
        public async Task<IActionResult> SendChatMessage([FromBody] ChatMessageRequest request)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("ReceiveChatMessage", new
                {
                    Sender = request.Sender,
                    Message = request.Message,
                    Timestamp = DateTime.UtcNow
                });

                _logger.LogInformation("Chat message from {Sender}: {Message}", request.Sender, request.Message);

                return Ok(new { Success = true, Message = "پیام ارسال شد" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending chat message");
                return BadRequest(new { Success = false, Message = "خطا در ارسال پیام" });
            }
        }
    }

    public class ViewerJoinRequest
    {
        public string? Name { get; set; }
    }

    public class ViewerLeaveRequest
    {
        public string ViewerId { get; set; } = string.Empty;
    }

    

    public class ChatMessageRequest
    {
        public string Sender { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
