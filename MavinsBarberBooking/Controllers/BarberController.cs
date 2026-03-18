using Dapper;
using MavinsBarberBooking.Models;
using MavinsBarberBooking.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
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
    }
}