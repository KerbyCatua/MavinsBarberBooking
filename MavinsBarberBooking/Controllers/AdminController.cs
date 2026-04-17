using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using Microsoft.ML;
using MavinsBarberBooking.Models.Entities;

namespace MavinsBarberBooking.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {

        private readonly IDbConnection _db;
        public AdminController(IDbConnection db) { _db = db; }


        // ================ GET METHODS ================    
        [HttpGet]
        public IActionResult Index()
        {
            // HIGHLIGHT: Retrieve specific columns using Dapper
            string sql = @"
                SELECT Id, FirstName, LastName, Email, PhoneNumber, IsActive, Role 
                FROM Users";

            // HIGHLIGHT: Map results to User model (ensure it has matching properties)
            var users = _db.Query<User>(sql).ToList();

            return View(users);
        }





        // ================ POST METHODS ================    
        [HttpPost]
        public IActionResult EditUser(int userId, string firstName, string lastName, string phoneNumber, string role)
        {
            // HIGHLIGHT: Update only the 4 allowed fields using Dapper
            string sql = @"
                UPDATE Users 
                SET FirstName = @FirstName, 
                    LastName = @LastName, 
                    PhoneNumber = @PhoneNumber, 
                    Role = @Role 
                WHERE Id = @Id";

            _db.Execute(sql, new
            {
                FirstName = firstName,
                LastName = lastName,
                PhoneNumber = phoneNumber,
                Role = role,
                Id = userId
            });

            return RedirectToAction("Index");
        }



        [HttpPost]
        public IActionResult DeleteUser(int userId)
        {
            // HIGHLIGHT: Delete user using Dapper
            string sql = "DELETE FROM Users WHERE Id = @Id";

            _db.Execute(sql, new { Id = userId });

            return RedirectToAction("Index");
        }



















    }
}