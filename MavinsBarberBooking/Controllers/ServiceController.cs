using Dapper;
using MavinsBarberBooking.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
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
                s.ServiceId, s.Name, s.DurationMinutes, s.Price, s.Details,
                b.Name AS BarberName
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
    }
}