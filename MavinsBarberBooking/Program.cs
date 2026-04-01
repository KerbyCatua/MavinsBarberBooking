using MavinsBarberBooking.Services;
using Microsoft.Data.SqlClient;
using System.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Dapper;
using Microsoft.AspNetCore.RateLimiting; // --- ADDED RATE LIMITING NAMESPACE ---
using System.Threading.RateLimiting;     // --- ADDED RATE LIMITING NAMESPACE ---

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register Dapper/SQL Connection
builder.Services.AddTransient<IDbConnection>((sp) =>
    new SqlConnection(builder.Configuration.GetConnectionString("DefaultConnection")));

// Email Service
builder.Services.AddScoped<IEmailService, EmailService>();

// Session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
});

// --- ADDED RATE LIMITING SERVICE CONFIGURATION ---
builder.Services.AddRateLimiter(options =>
{
    // Apply a global rate limit based on the client's IP address
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? httpContext.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100, // Maximum 100 requests...
                Window = TimeSpan.FromMinutes(1), // ...per 1 minute window
                QueueLimit = 0 // Do not queue excessive requests; reject immediately
            }));

    // Custom response when a user exceeds the limit
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync("Too many requests. Please try again later.", token);
    };
});
// --------------------------------------------------

// --- ADDED COOKIE AUTHENTICATION LOGIC HERE ---
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                // Get the SessionToken from the incoming cookie
                var sessionTokenClaim = context.Principal.FindFirst("SessionToken");

                if (sessionTokenClaim != null)
                {
                    // Use Dapper/IDbConnection to check the database
                    var connection = context.HttpContext.RequestServices.GetRequiredService<IDbConnection>();

                    // Look up the IsActive status of this specific session
                    string sql = "SELECT IsActive FROM UserSession WHERE SessionToken = @SessionToken";
                    var isActive = await connection.QueryFirstOrDefaultAsync<bool?>(sql, new { SessionToken = sessionTokenClaim.Value });

                    // If the session doesn't exist (null), or was marked inactive (false), reject the cookie
                    if (isActive == null || isActive == false)
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    }
                    else
                    {
                        // Optional: Update LastActivity time here
                        string updateSql = "UPDATE UserSession SET LastActivity = GETDATE() WHERE SessionToken = @SessionToken";
                        await connection.ExecuteAsync(updateSql, new { SessionToken = sessionTokenClaim.Value });
                    }
                }
            }
        };
    });
// ----------------------------------------------

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// --- ADDED RATE LIMITING MIDDLEWARE ---
app.UseRateLimiter(); // Must be called after UseRouting and before UseAuthentication/UseAuthorization
// --------------------------------------

// --- ADDED THIS LINE ---
app.UseAuthentication(); // Must come before UseAuthorization!
app.UseAuthorization();

app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");


// --- AUTO-CREATE TABLES FOR DAPPER ---
using (var scope = app.Services.CreateScope())
{
    var connection = scope.ServiceProvider.GetRequiredService<IDbConnection>();
    try
    {
        connection.Open();
        // Example: Create the Users table if it doesn't exist
        string UsersSql = @"
            -- 1. Create the Users table if it doesn't exist at all
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
            BEGIN
                CREATE TABLE Users (
                    Id INT PRIMARY KEY IDENTITY(1,1),
                    FirstName NVARCHAR(100) NOT NULL,
                    LastName NVARCHAR(100) NOT NULL,
                    Email NVARCHAR(150) NOT NULL UNIQUE,
                    PasswordHash NVARCHAR(255) NOT NULL,
                    PhoneNumber NVARCHAR(20) NULL,
                    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
                    IsActive BIT NOT NULL DEFAULT 1
                );
            END

            -- 2. If the table DOES exist, safely add the Role column if it's missing!
                IF NOT EXISTS (
                    SELECT * FROM sys.columns 
                    WHERE object_id = OBJECT_ID('Users') AND name = 'Role'
                )
                BEGIN
                    ALTER TABLE Users
                    ADD Role NVARCHAR(50) NOT NULL DEFAULT 'Customer';
                END
        ";

        string EmailVerificationsSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EmailVerifications')
            BEGIN
                CREATE TABLE EmailVerifications (
                    Id INT PRIMARY KEY IDENTITY(1,1),
                    Email NVARCHAR(255) NOT NULL,
                    VerificationCode NVARCHAR(10) NOT NULL,
                    CreatedAt DATETIME NOT NULL,
                    ExpiresAt DATETIME NOT NULL,
                    IsUsed BIT NOT NULL DEFAULT 0
                );
            END";

        string UserSessionSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserSession')
            BEGIN
                CREATE TABLE UserSession (
                    Id INT IDENTITY(1,1) PRIMARY KEY,

                    UserId INT NOT NULL,
                    SessionToken UNIQUEIDENTIFIER NOT NULL,
                    IpAddress NVARCHAR(50) NULL,
                    UserAgent NVARCHAR(255) NULL,
                    LastActivity DATETIME NOT NULL DEFAULT GETDATE(),
                    IsActive BIT NOT NULL DEFAULT 1,

                    CONSTRAINT FK_UserSession_Users
                        FOREIGN KEY (UserId)
                        REFERENCES Users(Id)
                        ON DELETE CASCADE
                );
            END
        ";

        string BarberSchemaSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Barbers')
            BEGIN
                CREATE TABLE Barbers (
                    BarberId INT PRIMARY KEY IDENTITY(1,1),
                    UserId NVARCHAR(450) NULL, /* Standard size if using ASP.NET Core Identity */
                    Name NVARCHAR(100) NOT NULL,
                    Rank NVARCHAR(50) NOT NULL, /* e.g., Head, Senior, Junior */
                    ProfileImage NVARCHAR(MAX) NULL,
                    AvailabilityStatus NVARCHAR(50) NOT NULL DEFAULT 'Available',
                    Rating DECIMAL(3, 2) NOT NULL DEFAULT 5.00
                );
            END

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Specialties')
            BEGIN
                CREATE TABLE Specialties (
                    SpecialtyId INT PRIMARY KEY IDENTITY(1,1),
                    Name NVARCHAR(100) NOT NULL UNIQUE
                );
            END

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BarberSpecialties')
            BEGIN
                CREATE TABLE BarberSpecialties (
                    BarberId INT NOT NULL,
                    SpecialtyId INT NOT NULL,
                    PRIMARY KEY (BarberId, SpecialtyId),
                    FOREIGN KEY (BarberId) REFERENCES Barbers(BarberId) ON DELETE CASCADE,
                    FOREIGN KEY (SpecialtyId) REFERENCES Specialties(SpecialtyId) ON DELETE CASCADE
                );
            END
        ";

        string ServiceSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Services')
            BEGIN
                CREATE TABLE Services (
                    ServiceId INT PRIMARY KEY IDENTITY(1,1),
                    Name NVARCHAR(100) NOT NULL,
                    DurationMinutes INT NOT NULL,
                    Price DECIMAL(10, 2) NOT NULL,
                    Details NVARCHAR(MAX) NULL,
                    ServiceImage NVARCHAR(MAX) NULL
                )
            END
            ELSE
            BEGIN
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Services') AND name = 'ServiceImage')
                BEGIN
                    ALTER TABLE Services ADD ServiceImage NVARCHAR(MAX) NULL
                END
            END
        ";

        string BarberServicesSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BarberServices')
            BEGIN
                CREATE TABLE BarberServices (
                    BarberId INT NOT NULL,
                    ServiceId INT NOT NULL,
                    PRIMARY KEY (BarberId, ServiceId),
                    FOREIGN KEY (BarberId) REFERENCES Barbers(BarberId) ON DELETE CASCADE,
                    FOREIGN KEY (ServiceId) REFERENCES Services(ServiceId) ON DELETE CASCADE
                );
            END
        ";

        // You can add more CREATE TABLE scripts here for your Barbershop system

        // Execute them in the CORRECT order (Parents first, then Children)
        using var command = connection.CreateCommand();

        // 1. Independent Tables (No Foreign Keys)
        command.CommandText = UsersSql;
        command.ExecuteNonQuery();

        command.CommandText = ServiceSql;
        command.ExecuteNonQuery();

        command.CommandText = BarberSchemaSql; // This creates Barbers & Specialties
        command.ExecuteNonQuery();

        command.CommandText = EmailVerificationsSql;
        command.ExecuteNonQuery();

        // 2. Dependent Tables (Have Foreign Keys)
        command.CommandText = UserSessionSql; // Needs Users
        command.ExecuteNonQuery();

        command.CommandText = BarberServicesSql; // Needs Barbers and Services
        command.ExecuteNonQuery();
    }
    catch (Exception ex)
    {
        // This will help you see if there's a connection error on startup
        Console.WriteLine("Database Check Failed: " + ex.Message);
    }
    finally
    {
        connection.Close();
    }
}
// -------------------------------------

app.Run();