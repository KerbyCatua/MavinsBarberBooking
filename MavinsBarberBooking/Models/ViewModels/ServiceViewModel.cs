namespace MavinsBarberBooking.Models.ViewModels
{
    public class ServiceViewModel
    {
        public int ServiceId { get; set; }
        public string Name { get; set; }
        public int DurationMinutes { get; set; }
        public decimal Price { get; set; }
        public string Details { get; set; }

        public int BarberId { get; set; }
        public string BarberName { get; set; }

        public string DurationDisplay
        {
            get
            {
                if (DurationMinutes < 60) return $"{DurationMinutes} mins";
                int hours = DurationMinutes / 60;
                int mins = DurationMinutes % 60;
                return mins > 0 ? $"{hours}hr {mins}mins" : $"{hours}hr";
            }
        }

        public string? ServiceImage { get; set; }
    }
}
