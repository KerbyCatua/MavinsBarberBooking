using Microsoft.AspNetCore.Mvc;

namespace MavinsBarberBooking.Controllers
{
    public class CustomerController : Controller
    {
        public IActionResult Profile()
        {
            return View();
        }
    }
}
