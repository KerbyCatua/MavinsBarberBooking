namespace MavinsBarberBooking.Models.ViewModels
{
    public class BarberViewModel
    {
        public int BarberId { get; set; }
        public string Name { get; set; }
        public string Rank { get; set; }
        public string ProfileImage { get; set; }
        public decimal Rating { get; set; }
        public List<string> Specialties { get; set; } = new List<string>();
    }
}
