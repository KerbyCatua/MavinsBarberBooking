using System.Diagnostics;
using MavinsBarberBooking.Models;
using Microsoft.AspNetCore.Mvc;

namespace MavinsBarberBooking.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public IActionResult ShareLink(string returnUrl = "/")
        {
            // Set the TempData message for your snackbar
            TempData["Message"] = "Website URL successfully copied to clipboard!";

            // Redirect back to the page the user was on
            return LocalRedirect(returnUrl);
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpGet]
        public IActionResult About()
        {
            return View();
        }


        [HttpGet]
        public IActionResult Team()
        {
            return View();
        }
    }
}
