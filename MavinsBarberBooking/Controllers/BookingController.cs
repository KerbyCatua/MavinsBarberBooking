using Microsoft.AspNetCore.Mvc;

namespace MavinsBarberBooking.Controllers
{
    public class BookingController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
