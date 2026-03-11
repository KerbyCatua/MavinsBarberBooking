namespace MavinsBarberBooking.Models.Entities
{
    public class UserSession
    {
        public int Id { get; set; }
        public string? UserId { get; set; } // Links to your User table
        public string SessionToken { get; set; } = null!; // A unique GUID for this specific login
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; } // e.g., "Chrome on Windows"
        public DateTime LastActivity { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
