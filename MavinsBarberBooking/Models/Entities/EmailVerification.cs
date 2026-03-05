namespace MavinsBarberBooking.Models.Entities
{
    public class EmailVerification
    {
        public int Id { get; set; }
        public string Email { get; set; } = null!;
        public string VerificationCode { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; }
    }
}
