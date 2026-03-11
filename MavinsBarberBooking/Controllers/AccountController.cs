using BCrypt.Net;
using Dapper;
using MavinsBarberBooking.Models.Entities;
using MavinsBarberBooking.Models.ViewModels;
using MavinsBarberBooking.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.ML;
using System.Data;
using System.Security.Claims;

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
            return RedirectToAction("VerifyEmail", new { email = model.Email } );
        }

        [HttpGet]
        public IActionResult VerifyEmail(string email)
        {
            
            if (string.IsNullOrEmpty(email)) return RedirectToAction("Register");

            if (TempData["ErrorMessage"] != null)
            {
                ModelState.AddModelError("VerificationCode", TempData["ErrorMessage"]?.ToString() ?? string.Empty);
            }

            ViewData["Email"] = email;
            return View();
        }

        [HttpPost]
        public IActionResult VerifyEmail(string email, string verificationCode)
        {
            var pending = _db.QueryFirstOrDefault<string>(
            "SELECT * FROM EmailVerifications WHERE Email = @Email",
            new { Email = email });

            if (pending == null)
            {
                ModelState.AddModelError("", "Registration not found. Please register again.");
                return RedirectToAction("Register");
            }

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
                TempData["ErrorMessage"] = "Invalid or expired verification code.";
                return RedirectToAction("VerifyEmail", new { email = email } );
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
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // 1. Validate username and password (Your existing logic)
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

            // --- SESSION TRACKING LOGIC STARTS HERE ---

            // 2. Generate a unique token for this specific login session
            var sessionToken = Guid.NewGuid().ToString();

            // 3. Get the user's IP Address and Browser info
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers["User-Agent"].ToString();

            // 4. Save to Database using Dapper
            var sql = @"
                INSERT INTO UserSession (UserId, SessionToken, IpAddress, UserAgent, LastActivity, IsActive)
                VALUES (@UserId, @SessionToken, @IpAddress, @UserAgent, @LastActivity, @IsActive)";

                    _db.Execute(sql, new
                    {
                        UserId = user!.Id, // Assuming your User model has an 'Id' property
                        SessionToken = sessionToken,
                        IpAddress = ipAddress,
                        UserAgent = userAgent,
                        LastActivity = DateTime.UtcNow,
                        IsActive = true
                    });

            // 5. Add the SessionToken to the user's Claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.FirstName), // This populates @User.Identity.Name
                new Claim(ClaimTypes.Email, user.Email), // or user.UserName if you have one
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim("SessionToken", sessionToken) // Custom Claim for tracking!
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            // 6. Issue the authentication cookie to the browser
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            // --- SESSION TRACKING LOGIC ENDS HERE ---

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


        [HttpPost]
        public async Task<IActionResult> RevokeSession(int sessionId)
        {
            // 1. Get the currently logged-in user's ID from the cookie claims
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (int.TryParse(userIdString, out int userId))
            {
                // 2. Use Dapper to set IsActive to 0 (false) for that specific session
                // We include UserId in the WHERE clause so a user can't maliciously delete someone else's session
                string sql = @"
                UPDATE UserSession 
                SET IsActive = 0 
                WHERE Id = @SessionId AND UserId = @UserId";

                await _db.ExecuteAsync(sql, new { SessionId = sessionId, UserId = userId });
            }

            return RedirectToAction("ActiveSessions");
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            // 1. Get the current SessionToken from the user's claims
            var sessionToken = User.FindFirstValue("SessionToken");

            if (!string.IsNullOrEmpty(sessionToken))
            {
                // 2. Mark THIS specific session as inactive in the database
                string sql = "UPDATE UserSession SET IsActive = 0 WHERE SessionToken = @SessionToken";
                await _db.ExecuteAsync(sql, new { SessionToken = sessionToken });
            }

            // 3. Delete the authentication cookie from the user's browser
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // 4. Redirect them back to the home page or login page
            return RedirectToAction("Login", "Account");
        }

    }
}
