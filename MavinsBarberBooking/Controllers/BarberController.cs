using Dapper;
using MavinsBarberBooking.Models;
using MavinsBarberBooking.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.ML;
using System.Data;
using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;

namespace MavinsBarberBooking.Controllers
{
    public class BarberController : Controller
    {
        private readonly IDbConnection _db;
        private readonly IWebHostEnvironment _env;

        public BarberController(IDbConnection db, IWebHostEnvironment env) 
        { 
            _db = db;
            _env = env;
        }



        // ================== GET METHODS ==================

        public IActionResult Index()
        {
            var sql = @"
                SELECT 
                    b.BarberId, b.Name, b.Rank, b.ProfileImage, b.Rating,
                    s.Name AS SpecialtyName
                FROM Barbers b
                LEFT JOIN BarberSpecialties bs ON b.BarberId = bs.BarberId
                LEFT JOIN Specialties s ON bs.SpecialtyId = s.SpecialtyId";

            var barberDictionary = new Dictionary<int, BarberViewModel>();

            _db.Query<BarberViewModel, string, BarberViewModel>(
                sql,
                (barber, specialtyName) =>
                {
                    if (!barberDictionary.TryGetValue(barber.BarberId, out var currentBarber))
                    {
                        currentBarber = barber;
                        barberDictionary.Add(currentBarber.BarberId, currentBarber);
                    }

                    if (!string.IsNullOrEmpty(specialtyName))
                    {
                        currentBarber.Specialties.Add(specialtyName);
                    }

                    return currentBarber;
                },
                splitOn: "SpecialtyName"
            );

            return View(barberDictionary.Values.ToList());
        }

        // Barber Profile
        [HttpGet]
        [Authorize(Roles = "Barber,Admin")]
        public async Task<IActionResult> Profile()
        {
            // Assuming the logged-in user's primary key (Id) from the Users table is stored in the NameIdentifier claim
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account");

            string barberSql = "SELECT BarberId, Name, Rank, ProfileImage, AvailabilityStatus FROM Barbers WHERE UserId = @UserId";
            var barber = await _db.QuerySingleOrDefaultAsync<BarberProfileViewModel>(barberSql, new { UserId = userId });

            if (barber == null)
            {
                // Handle case where user is a Barber role but has no Barber profile record yet
                return NotFound("Barber profile not found.");
            }

            string servicesSql = @"
                SELECT s.ServiceId, s.Name, s.DurationMinutes, s.Price, s.Details, s.ServiceImage 
                FROM Services s
                INNER JOIN BarberServices bs ON s.ServiceId = bs.ServiceId
                WHERE bs.BarberId = @BarberId";

            barber.Services = await _db.QueryAsync<BarberServiceViewModel>(servicesSql, new { BarberId = barber.BarberId });

            string slotsSql = @"
                SELECT SlotId, StartTime, EndTime 
                FROM TimeSlots 
                WHERE BarberId = @BarberId 
                ORDER BY StartTime";

            barber.TimeSlots = await _db.QueryAsync<BarberTimeSlotViewModel>(slotsSql, new { BarberId = barber.BarberId });

            return View(barber);
        }








        // ================== POST METHODS ==================


        // ================== SLOTS ==================

        [HttpPost]
        public async Task<IActionResult> AddTimeSlot(int barberId, DateTime slotDate, TimeSpan startTime, TimeSpan endTime)
        {
            var fullStartTime = slotDate.Add(startTime);
            var fullEndTime = slotDate.Add(endTime);

            string sql = @"
                INSERT INTO TimeSlots (BarberId, StartTime, EndTime) 
                VALUES (@BarberId, @StartTime, @EndTime)";

            await _db.ExecuteAsync(sql, new { BarberId = barberId, StartTime = fullStartTime, EndTime = fullEndTime });

            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteTimeSlot(int slotId)
        {
            string sql = "DELETE FROM TimeSlots WHERE SlotId = @SlotId";
            await _db.ExecuteAsync(sql, new { SlotId = slotId });

            return RedirectToAction(nameof(Profile));
        }







        // ================== SERVICE ==================
        // --- ADD SERVICE ---
        [HttpPost]
        // ADDED '?' to details and imageFile
        public async Task<IActionResult> AddService(int barberId, string name, int durationMinutes, decimal price, string? details, IFormFile? imageFile, bool useDefaultImage)
        {
            string? serviceImagePath = null;

            if (!useDefaultImage && imageFile != null && imageFile.Length > 0)
            {
                var fileName = $"service_{Guid.NewGuid()}{Path.GetExtension(imageFile.FileName)}";

                var folderPath = Path.Combine(_env.WebRootPath, "images", "services");
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                var filePath = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }
                serviceImagePath = $"/images/services/{fileName}";
            }

            string insertServiceSql = @"
                INSERT INTO Services (Name, DurationMinutes, Price, Details, ServiceImage)
                VALUES (@Name, @DurationMinutes, @Price, @Details, @ServiceImage);
                SELECT CAST(SCOPE_IDENTITY() as int);";

            int newServiceId = await _db.ExecuteScalarAsync<int>(insertServiceSql, new
            {
                Name = name,
                DurationMinutes = durationMinutes,
                Price = price,
                Details = details ?? (object)DBNull.Value,
                ServiceImage = serviceImagePath ?? (object)DBNull.Value
            });

            string linkServiceSql = @"
                INSERT INTO BarberServices (BarberId, ServiceId)
                VALUES (@BarberId, @ServiceId)";

            await _db.ExecuteAsync(linkServiceSql, new { BarberId = barberId, ServiceId = newServiceId });

            return RedirectToAction(nameof(Profile));
        }

        // --- EDIT SERVICE ---
        [HttpPost]
        // ADDED '?' to details and imageFile
        public async Task<IActionResult> EditService(int serviceId, string name, int durationMinutes, decimal price, string? details, IFormFile? imageFile)
        {
            string sql = @"
                UPDATE Services 
                SET Name = @Name, DurationMinutes = @DurationMinutes, Price = @Price, Details = @Details
                WHERE ServiceId = @ServiceId";

            await _db.ExecuteAsync(sql, new
            {
                ServiceId = serviceId,
                Name = name,
                DurationMinutes = durationMinutes,
                Price = price,
                Details = details ?? (object)DBNull.Value
            });

            if (imageFile != null && imageFile.Length > 0)
            {
                var fileName = $"service_{serviceId}_{Guid.NewGuid()}{Path.GetExtension(imageFile.FileName)}";

                var folderPath = Path.Combine(_env.WebRootPath, "images", "services");
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                var filePath = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                string imageSql = "UPDATE Services SET ServiceImage = @ServiceImage WHERE ServiceId = @ServiceId";
                await _db.ExecuteAsync(imageSql, new { ServiceImage = $"/images/services/{fileName}", ServiceId = serviceId });
            }

            return RedirectToAction(nameof(Profile));
        }

        // --- DELETE SERVICE ---
        [HttpPost]
        public async Task<IActionResult> DeleteService(int serviceId)
        {
            // Because of ON DELETE CASCADE in BarberServices, deleting from Services will also remove the link
            string sql = "DELETE FROM Services WHERE ServiceId = @ServiceId";
            await _db.ExecuteAsync(sql, new { ServiceId = serviceId });

            return RedirectToAction(nameof(Profile));
        }










        // Update profile image for barber
        [HttpPost]
        [Authorize(Roles = "Barber,Admin")]
        public async Task<IActionResult> UpdateProfileImage(int barberId, IFormFile imageFile)
        {
            if (imageFile != null && imageFile.Length > 0)
            {
                var fileName = $"barber_{barberId}_{Guid.NewGuid()}{Path.GetExtension(imageFile.FileName)}";
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/barbers", fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                string sql = "UPDATE Barbers SET ProfileImage = @ProfileImage WHERE BarberId = @BarberId";
                await _db.ExecuteAsync(sql, new { ProfileImage = $"/images/barbers/{fileName}", BarberId = barberId });
            }
            return RedirectToAction("Index");
        }


    }
}