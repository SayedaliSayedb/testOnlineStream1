using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using WebApplication1.Hubs;

namespace WebApplication1.Controllers
{
    public class AdminController : Controller
    {
        private readonly IHubContext<WebRTCHub> _hubContext;

        public AdminController(IHubContext<WebRTCHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> KickUser([FromBody] KickUserRequest request)
        {
            try
            {
                await _hubContext.Clients.Client(request.ConnectionId).SendAsync("Kicked", "شما از سمت ادمین اخراج شدید");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ToggleChat([FromBody] ToggleRequest request)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("ChatStatusUpdated", request.Enabled);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ToggleHearts([FromBody] ToggleRequest request)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("HeartsStatusUpdated", request.Enabled);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ClearChatHistory([FromBody] ClearChatRequest request)
        {
            try
            {
                await _hubContext.Clients.Group(request.StreamId).SendAsync("ChatHistoryCleared");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult GetUsers()
        {
            try
            {
                var hub = new WebRTCHub();
                var users = hub.GetUsers();
                return Json(new { success = true, data = users });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message, data = new List<object>() });
            }
        }

        [HttpGet]
        public IActionResult GetStats()
        {
            try
            {
                var hub = new WebRTCHub();
                var stats = hub.GetAdminStats();
                return Json(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult GetChatHistory([FromQuery] string streamId)
        {
            try
            {
                var hub = new WebRTCHub();
                var history = hub.GetChatHistory(streamId);
                return Json(new { success = true, data = history });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message, data = new List<object>() });
            }
        }
    }

    public class KickUserRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
    }

    public class ToggleRequest
    {
        public bool Enabled { get; set; }
    }

    public class ClearChatRequest
    {
        public string StreamId { get; set; } = string.Empty;
    }
}
