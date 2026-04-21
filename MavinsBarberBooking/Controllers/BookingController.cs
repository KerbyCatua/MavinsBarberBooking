using Microsoft.AspNetCore.Mvc;
using System.Data;
using Dapper;
using System.ComponentModel.DataAnnotations;
using MavinsBarberBooking.Models;
using MavinsBarberBooking.Models.Entities;
using MavinsBarberBooking.Models.ViewModels;


namespace MavinsBarberBooking.Controllers
{
    public class BookingController : Controller
    {
        private readonly IDbConnection _db;
        public BookingController(IDbConnection db) { _db = db; }

        // ==================== HTTP GET ====================

        [HttpGet]
        public IActionResult Payment()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Confirm(int serviceId, int barberId, string selectedDate, string selectedTime)
        {
            // Fetch Service details
            var service = await _db.QueryFirstOrDefaultAsync(
                "SELECT * FROM Services WHERE ServiceId = @ServiceId", new { ServiceId = serviceId });

            // Fetch Barber details
            var barber = await _db.QueryFirstOrDefaultAsync(
                "SELECT * FROM Barbers WHERE BarberId = @BarberId", new { BarberId = barberId });

            if (service == null || barber == null)
            {
                return NotFound();
            }

            var model = new BookingViewModel
            {
                ServiceId = serviceId,
                ServiceName = service?.Name,
                Price = service != null ? (decimal)service.Price : 0,
                DurationMinutes = service != null ? (int)service.DurationMinutes : 0,
                BarberId = barberId,
                BarberName = barber?.Name,
                SelectedDate = selectedDate,
                SelectedTime = selectedTime
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Index(int serviceId, int barberId)
        {
            // Role-based access control: Only Customers can book appointments
            if (User.IsInRole("Admin")) return RedirectToAction("DeniedForAdmin", "Access");
            if (User.IsInRole("Barber")) return RedirectToAction("DeniedForBarber", "Access");

            // Fallback to fetch barber if not explicitly provided
            if (barberId == 0)
            {
                barberId = await _db.QueryFirstOrDefaultAsync<int>(
                    "SELECT TOP 1 BarberId FROM BarberServices WHERE ServiceId = @ServiceId",
                    new { ServiceId = serviceId });
            }

            // Fetch Service details
            var service = await _db.QueryFirstOrDefaultAsync(
                "SELECT * FROM Services WHERE ServiceId = @ServiceId", new { ServiceId = serviceId });

            // Fetch Barber details
            var barber = await _db.QueryFirstOrDefaultAsync(
                "SELECT * FROM Barbers WHERE BarberId = @BarberId", new { BarberId = barberId });

            if (service == null || barber == null)
            {
                return NotFound();
            }

            // Fetch Time Slots for the mapped Barber
            var timeSlots = await _db.QueryAsync<TimeSlotModel>(
                "SELECT StartTime, EndTime FROM TimeSlots WHERE BarberId = @BarberId",
                new { BarberId = barberId });

            var model = new BookingViewModel
            {
                ServiceId = serviceId,
                ServiceName = service?.Name,
                Price = service != null ? (decimal)service.Price : 0,
                DurationMinutes = service != null ? (int)service.DurationMinutes : 0,
                BarberId = barberId,
                BarberName = barber?.Name,
                TimeSlots = timeSlots.ToList()
            };

            return View(model);
        }

    }






    // View Models placed here for simplicity (can be moved to their own folder)
    public class BookingViewModel
    {
        public int ServiceId { get; set; }
        public string? ServiceName { get; set; }
        public decimal Price { get; set; }
        public int DurationMinutes { get; set; }
        public int BarberId { get; set; }
        public string? BarberName { get; set; }
        public List<TimeSlotModel> TimeSlots { get; set; } = new();


        // Added for the Confirm step
        [Required]
        public string? SelectedDate { get; set; }
        [Required]
        public string? SelectedTime { get; set; }

        // Customer Details Validation
        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Full Name must be between 2 and 100 characters.")]
        [RegularExpression(@"^[a-zA-Z\s\-\.]+$", ErrorMessage = "Name contains invalid characters.")] // Protects input
        public string? CustomerName { get; set; }

        [Required(ErrorMessage = "Phone Number is required.")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Please enter a valid 10-digit mobile number (e.g., 9123456789).")]
        public string? CustomerPhone { get; set; }

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
        public string? CustomerEmail { get; set; }

        [StringLength(500, ErrorMessage = "Comments cannot exceed 500 characters.")]
        public string? Comments { get; set; }
    }

    public class TimeSlotModel
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }

}