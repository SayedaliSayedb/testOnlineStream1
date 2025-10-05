using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WebApplication1.Hubs;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHubContext<WebRTCHub> _hubContext;

        public HomeController(IHubContext<WebRTCHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Stream()
        {
            return View();
        }

        public IActionResult Viewer()
        {
            return View();
        }

        [HttpGet]
        public IActionResult GetStreams()
        {
            try
            {
                // ایجاد یک instance از هاب برای دسترسی به متدها
                var hub = new WebRTCHub();
                var streams = hub.GetAvailableStreams();
                return Json(streams);
            }
            catch (Exception ex)
            {
                return Json(new List<StreamInfo>());
            }
        }


        [HttpGet]
        public IActionResult GetStats()
        {
            try
            {
                // ایجاد یک instance از هاب برای دسترسی به متدها
                var hub = new WebRTCHub();
                var stats = hub.GetStats();
                return Json(stats);
            }
            catch (Exception ex)
            {
                return Json(new StreamStats());
            }
        }


        [HttpPost]
        public async Task<IActionResult> KickUser(string connectionId)
        {
            try
            {
                await _hubContext.Clients.Client(connectionId).SendAsync("Kicked", "شما از سمت ادمین اخراج شدید");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ToggleChat(bool enabled)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("ChatStatusUpdated", enabled);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }
    }

    // Extension method to access hub from context
    public static class HubContextExtensions
    {
        public static T? GetHub<T>(this IHubContext<T> context) where T : Hub
        {
            var provider = context.GetType().GetProperty("ServiceProvider")?.GetValue(context) as IServiceProvider;
            return provider?.GetService<T>();
        }
    }

}
