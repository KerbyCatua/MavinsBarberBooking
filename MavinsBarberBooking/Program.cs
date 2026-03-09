using MavinsBarberBooking.Services;
using Microsoft.Data.SqlClient;
using System.Data;

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

var app = builder.Build();



// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");




// --- AUTO-CREATE TABLES FOR DAPPER ---
using (var scope = app.Services.CreateScope())
{
    var connection = scope.ServiceProvider.GetRequiredService<IDbConnection>();
    try
    {
        connection.Open();
        // Example: Create the Users table if it doesn't exist
        string UsersSql = @"
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
            END";

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

        // You can add more CREATE TABLE scripts here for your Barbershop system


        // Execute them one by one
        using var command = connection.CreateCommand();

        command.CommandText = UsersSql;
        command.ExecuteNonQuery(); // Executes Users creation

        command.CommandText = EmailVerificationsSql;
        command.ExecuteNonQuery(); // Executes EmailVerifications creation
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