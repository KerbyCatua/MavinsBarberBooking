using Dapper;
using MavinsBarberBooking.Models;
using MavinsBarberBooking.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.ML;
using System.Data;

namespace MavinsBarberBooking.Controllers
{
    public class BarberController : Controller
    {
        private readonly IDbConnection _db;

        public BarberController(IDbConnection db) { _db = db; }

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