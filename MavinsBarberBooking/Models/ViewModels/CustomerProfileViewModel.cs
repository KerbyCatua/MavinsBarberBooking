namespace MavinsBarberBooking.Models.ViewModels
{
    public class CustomerProfileViewModel
    {
        public int UserId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsActive { get; set; }
        public string? Role { get; set; }

        public List<BookingViewModel> UpcomingAppointments { get; set; } = new List<BookingViewModel>();
        public List<BookingViewModel> BookingHistory { get; set; } = new List<BookingViewModel>();
    }

    public class BookingViewModel
    {
        public int BookingId { get; set; }
        public string? ServiceName { get; set; }
        public string? BarberName { get; set; }
        public DateTime BookingDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public string? Status { get; set; }
    }
}
