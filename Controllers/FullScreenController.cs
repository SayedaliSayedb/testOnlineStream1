using Microsoft.AspNetCore.Mvc;

namespace WebApplication1.Controllers
{
    public class FullScreenController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Viewer()
        {
            return View();
        }
    }
}
