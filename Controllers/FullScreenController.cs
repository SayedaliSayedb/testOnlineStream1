using Microsoft.AspNetCore.Mvc;

namespace WebApplication1.Controllers
{
    public class FullScreenController : Controller
    {
        public IActionResult Index()
        {
            ViewBag.Image = "https://thumb.ac-illust.com/51/51e1c1fc6f50743937e62fca9b942694_t.jpeg";
                           return View();
        }

        public IActionResult Viewer()
        {
            return View();
        }
    }
}
