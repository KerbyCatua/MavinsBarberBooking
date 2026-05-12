using System.Text.Json;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using Dapper;
using System.ComponentModel.DataAnnotations;
using MavinsBarberBooking.Models;
using MavinsBarberBooking.Models.Entities;
using MavinsBarberBooking.Models.ViewModels;
using System.Security.Claims;

namespace MavinsBarberBooking.Controllers
{
    public class BookingController : Controller
    {
        private readonly IDbConnection _db;
        private readonly IConfiguration _config;
        public BookingController(IDbConnection db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        // ==================== HTTP GET ====================
        [HttpGet]
        public IActionResult Payment()
        {
            // You may optionally redirect to Index if accessed directly without data
            TempData["Message"] = "Please select a service and time slot before proceeding to payment.";
            return RedirectToAction("Index", "Home");
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




        // ==================== HTTP POST ====================
        [HttpPost]
        public async Task<IActionResult> Payment(int serviceId, int barberId, string selectedDate, string selectedTime)
        {
            // Parse strings to DateTime and TimeSpan based on what the frontend sent
            if (!DateTime.TryParse(selectedDate, out DateTime bookingDate))
            {
                return BadRequest("Invalid Date format.");
            }
            if (!DateTime.TryParse(selectedTime, out DateTime parsedTime))
            {
                return BadRequest("Invalid Time format.");
            }
            TimeSpan startTime = parsedTime.TimeOfDay;

            // Fetch Service details to calculate EndTime
            var service = await _db.QueryFirstOrDefaultAsync(
                "SELECT * FROM Services WHERE ServiceId = @ServiceId", new { ServiceId = serviceId });

            if (service == null) return NotFound("Service not found");

            TimeSpan endTime = startTime.Add(TimeSpan.FromMinutes((int)service.DurationMinutes));


            // 1. Math for Pass-On Fee (GCash fee is 2.5%)
            // TENTATIVE AMOUNT FOR TESTING PURPOSES ONLY - PLEASE REPLACE WITH ACTUAL SERVICE PRICE
            decimal targetAmount = 01.00m; // The exact amount YOU receive
            decimal feePercentage = 0.000m;
            decimal grossAmount = Math.Round(targetAmount / (1 - feePercentage), 2); // Customer pays ~₱51.28
            decimal feeAmount = grossAmount - targetAmount; // ~₱1.28
            int amountInCents = (int)(grossAmount * 100);


            // 2. Save Pending Booking to Database
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var customerId = await _db.QuerySingleOrDefaultAsync<int>("SELECT CustomerId FROM Customers WHERE UserId = @UserId", new { UserId = userId });
            if (customerId == 0) return BadRequest("Customer profile not found for this user.");


            string insertBookingSql = @"
                INSERT INTO Bookings (CustomerId, BarberId, ServiceId, BookingDate, StartTime, EndTime, Status) 
                OUTPUT INSERTED.BookingId
                VALUES (@CustomerId, @BarberId, @ServiceId, @BookingDate, @StartTime, @EndTime, 'Pending Payment');";

            var bookingId = await _db.QuerySingleAsync<int>(insertBookingSql, new { customerId, barberId, serviceId, bookingDate, startTime, endTime });

            string paySql = "INSERT INTO Payments (BookingId, Amount, PaymentMethod, PaymentStatus) VALUES (@BookingId, @Amount, 'GCash', 0)";
            await _db.ExecuteAsync(paySql, new { BookingId = bookingId, Amount = grossAmount });


            // 3. Call PayMongo API
            var secretKey = _config["PayMongo:SecretKey"];
            var payload = new
            {
                data = new
                {
                    attributes = new
                    {
                        amount = amountInCents,
                        description = "Mavins Barbershop - Downpayment",
                        remarks = bookingId.ToString(),
                        payment_method_allowed = new[] { "gcash", "paymaya" },
                        currency = "PHP"
                    }
                }
            };

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(secretKey + ":")));

            var response = await client.PostAsJsonAsync("https://api.paymongo.com/v1/links", payload);
            var responseString = await response.Content.ReadAsStringAsync();
            using var jsonDocument = JsonDocument.Parse(responseString);
            var checkoutUrl = jsonDocument.RootElement.GetProperty("data").GetProperty("attributes").GetProperty("checkout_url").GetString();

            string barberQuery = "SELECT Name FROM Barbers WHERE BarberId = @BarberId";
            var realBarberName = await _db.QueryFirstOrDefaultAsync<string>(barberQuery, new { BarberId = barberId });

            // 4. Pass the calculated data to the View Model
            var vm = new PaymentViewModel
            {
                ServiceName = service.Name,
                BarberName = realBarberName ?? "Unknown Barber",
                BaseAmount = targetAmount,
                FeeAmount = feeAmount,
                TotalAmount = grossAmount,
                CheckoutUrl = checkoutUrl
            };

            return View(vm);
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