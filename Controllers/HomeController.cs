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
            var hub = _hubContext.GetHub<WebRTCHub>();
            if (hub != null)
            {
                var streams = hub.GetAvailableStreams();
                return Json(streams);
            }
            return Json(new List<StreamInfo>());
        }

        [HttpGet]
        public IActionResult GetStats()
        {
            var hub = _hubContext.GetHub<WebRTCHub>();
            if (hub != null)
            {
                var stats = hub.GetStats();
                return Json(stats);
            }
            return Json(new StreamStats());
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
