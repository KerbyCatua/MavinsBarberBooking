using Microsoft.AspNetCore.Mvc;

namespace MavinsBarberBooking.Controllers
{
    public class ServiceController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
