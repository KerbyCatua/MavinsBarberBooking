using System.ComponentModel.DataAnnotations;

namespace MavinsBarberBooking.Models.ViewModels
{
    public class RegisterViewModel
    {
        [Required]
        public string FirstName { get; set; } = null!;

        [Required]
        public string LastName { get; set; } = null!;

        [Required]
        public string Email { get; set; } = null!;

        [Required]
        [RegularExpression(@"^.{8,}$", ErrorMessage = "Password must be at least 8 characters long.")]
        public string Password { get; set; } = null!;

        [Required]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = null!;

        // Add this for verification step
        public string? VerificationCode { get; set; }
    }
}