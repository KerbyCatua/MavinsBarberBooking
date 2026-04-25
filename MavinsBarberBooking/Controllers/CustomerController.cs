using Dapper;
using MavinsBarberBooking.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Security.Claims;

namespace MavinsBarberBooking.Controllers
{
    [Authorize]
    public class CustomerController : Controller
    {
        // Dependency injection of the database connection
        private readonly IDbConnection _db;
        public CustomerController(IDbConnection db) { _db = db; }

        // Role-based access control: Only Customers can access the profile page
        //if (User.IsInRole("Admin")) return RedirectToAction("DeniedForAdmin", "Access");
        //if (User.IsInRole("Barber")) return RedirectToAction("DeniedForBarber", "Access");

        public async Task<IActionResult> Profile()
        {
            var userEmail = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

            if (string.IsNullOrEmpty(userEmail)) return RedirectToAction("Login", "Account");

            // 1. Fetch User Data
            var userQuery = "SELECT Id as UserId, FirstName, LastName, Email, PhoneNumber, IsActive, Role FROM Users WHERE Email = @Email";
            var user = await _db.QueryFirstOrDefaultAsync<CustomerProfileViewModel>(userQuery, new { Email = userEmail });

            if (user == null) return NotFound();

            // 2. Fetch Bookings (Joined with Customers, Services, Barbers)
            var bookingsQuery = @"
                SELECT 
                    b.BookingId,
                    s.Name as ServiceName,
                    bar.Name as BarberName,
                    b.BookingDate,
                    b.StartTime,
                    b.Status
                FROM Bookings b
                INNER JOIN Customers c ON b.CustomerId = c.CustomerId
                INNER JOIN Services s ON b.ServiceId = s.ServiceId
                INNER JOIN Barbers bar ON b.BarberId = bar.BarberId
                WHERE c.UserId = @UserId
                ORDER BY b.BookingDate DESC, b.StartTime DESC";

            var allBookings = (await _db.QueryAsync<MavinsBarberBooking.Models.ViewModels.BookingViewModel>(bookingsQuery, new { UserId = user.UserId })).ToList();

            // Split into Upcoming and History
            user.UpcomingAppointments = allBookings.Where(b => b.Status == "Upcoming").ToList();
            user.BookingHistory = allBookings.Where(b => b.Status == "Completed" || b.Status == "Cancelled").ToList();

            return View(user);
        }

    }
}
