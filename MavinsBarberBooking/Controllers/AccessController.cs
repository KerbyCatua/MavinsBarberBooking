using Microsoft.AspNetCore.Mvc;

namespace MavinsBarberBooking.Controllers
{
    public class AccessController : Controller
    {

        [HttpGet]
        public IActionResult DeniedForAdmin()
        {
            return View();
        }

        [HttpGet]
        public IActionResult DeniedForBarber()
        {
            return View();
        }

        [HttpGet]
        public IActionResult DeniedForCustomer()
        {
            return View();
        }

    }
}
