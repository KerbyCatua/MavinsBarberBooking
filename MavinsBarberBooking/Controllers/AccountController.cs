using Microsoft.AspNetCore.Mvc;
using MavinsBarberBooking.Models.ViewModels;
using MavinsBarberBooking.Models.Entities;
using MavinsBarberBooking.Services;

using BCrypt.Net;

using Dapper;
using System.Data;

namespace MavinsBarberBooking.Controllers
{
    public class AccountController : Controller
    {

        // Dependency injection for database connection and email service
        private readonly IDbConnection _db;
        private readonly IEmailService _emailService;
        // Constructor to initialize the dependencies
        public AccountController(IDbConnection db, IEmailService emailService) 
        {
            _db = db;
            _emailService = emailService;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var existingUser = _db.QueryFirstOrDefault<string>(
                "SELECT Email FROM Users WHERE Email = @Email", new { model.Email });

            if (existingUser != null)
            {
                ModelState.AddModelError("Email", "Email already exists.");
                return View(model);
            }

            // Generate verification code
            string verificationCode = GenerateVerificationCode();

            // Store verification code in database
            string sql = @"INSERT INTO EmailVerifications
                           (Email, VerificationCode, CreatedAt, ExpiresAt, IsUsed)
                           VALUES
                           (@Email, @VerificationCode, GETDATE(), DATEADD(MINUTE, 15, GETDATE()), 0)";

            _db.Execute(sql, new
            {
                model.Email,
                VerificationCode = verificationCode
            });

            // Send verification email
            await _emailService.SendVerificationEmailAsync(model.Email, verificationCode);

            // Store the registration data in session temporarily
            HttpContext.Session.SetString("TempEmail", model.Email);
            HttpContext.Session.SetString("TempFirstName", model.FirstName);
            HttpContext.Session.SetString("TempLastName", model.LastName);
            HttpContext.Session.SetString("TempPassword", model.Password);

            TempData["SuccessMessage"] = "Verification code sent to your email. Please check your inbox.";
            return RedirectToAction("VerifyEmail");
        }

        [HttpGet]
        public IActionResult VerifyEmail()
        {
            ViewData["Email"] = HttpContext.Session.GetString("TempEmail") ?? null;
            return View();
        }

        [HttpPost]
        public IActionResult VerifyEmail(string verificationCode)
        {
            var email = HttpContext.Session.GetString("TempEmail");

            if (string.IsNullOrEmpty(email))
            {
                ModelState.AddModelError("", "Session expired. Please register again.");
                return RedirectToAction("Register");
            }

            // Verify the code
            var verification = _db.QueryFirstOrDefault<EmailVerification>(
                @"SELECT * FROM EmailVerifications 
                  WHERE Email = @Email 
                  AND VerificationCode = @VerificationCode 
                  AND IsUsed = 0 
                  AND ExpiresAt > GETDATE()",
                new { Email = email, VerificationCode = verificationCode });

            if (verification == null)
            {
                ModelState.AddModelError("VerificationCode", "Invalid or expired verification code.");
                return View();
            }

            // Mark code as used
            _db.Execute(
                "UPDATE EmailVerifications SET IsUsed = 1 WHERE Id = @Id",
                new { verification.Id });

            // Create the user account
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(HttpContext.Session.GetString("TempPassword"));

            string sql = @"INSERT INTO Users
                           (FirstName, LastName, Email, PasswordHash, PhoneNumber, CreatedAt, IsActive)
                           VALUES
                           (@FirstName, @LastName, @Email, @PasswordHash, @PhoneNumber, GETDATE(), 1)";

            _db.Execute(sql, new
            {
                FirstName = HttpContext.Session.GetString("TempFirstName"),
                LastName = HttpContext.Session.GetString("TempLastName"),
                Email = email,
                PasswordHash = passwordHash,
                PhoneNumber = ""
            });

            // Clear session
            HttpContext.Session.Clear();

            TempData["SuccessMessage"] = "Email verified successfully! You can now login.";
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = _db.QueryFirstOrDefault<User>(
                "SELECT * FROM Users WHERE Email = @Email", new { model.Email });

            if (user == null)
            {
                ModelState.AddModelError("Email", "Email not found. Please proceed to register.");
            }
            else if (!BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
            {
                ModelState.AddModelError("Password", "Invalid password.");
            }

            if (!ModelState.IsValid) return View(model);

            return RedirectToAction("Index", "Home");
        }

        private string GenerateVerificationCode()
        {
            const string chars = "0123456789";
            var random = new Random();
            return new string(Enumerable.Range(0, 6)
                .Select(_ => chars[random.Next(chars.Length)])
                .ToArray());
        }

    }
}
