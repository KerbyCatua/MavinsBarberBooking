using Dapper;
using MavinsBarberBooking.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ML;
using System.Data;

namespace MavinsBarberBooking.Controllers
{
    public class ServiceController : Controller
    {
        private readonly IDbConnection _db;

        public ServiceController(IDbConnection db)
        {
            _db = db;
        }

        public IActionResult Index(int? barberId, string barberName)
        {
            ViewBag.IsAllServices = !barberId.HasValue || barberId == 0;
            ViewBag.HeaderName = ViewBag.IsAllServices ? "All Services" : barberName;

            string sql = @"
            SELECT 
                s.ServiceId, s.Name, s.DurationMinutes, s.Price, s.Details, s.ServiceImage,
                b.Name AS BarberName, b.BarberId -- <-- Ensure b.BarberId is selected
            FROM Services s
            INNER JOIN BarberServices bs ON s.ServiceId = bs.ServiceId
            INNER JOIN Barbers b ON bs.BarberId = b.BarberId";

            if (!ViewBag.IsAllServices)
            {
                sql += " WHERE b.BarberId = @BarberId";
                var services = _db.Query<ServiceViewModel>(sql, new { BarberId = barberId }).ToList();
                return View(services);
            }
            else
            {
                var allServices = _db.Query<ServiceViewModel>(sql).ToList();
                return View(allServices);
            }
        }


        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateServiceImage(int serviceId, IFormFile imageFile)
        {
            if (imageFile != null && imageFile.Length > 0)
            {
                var fileName = $"service_{serviceId}_{Guid.NewGuid()}{Path.GetExtension(imageFile.FileName)}";
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/services", fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                string sql = "UPDATE Services SET ServiceImage = @ServiceImage WHERE ServiceId = @ServiceId";
                await _db.ExecuteAsync(sql, new { ServiceImage = $"/images/services/{fileName}", ServiceId = serviceId });
            }
            return RedirectToAction("Index");
        }


    }
}