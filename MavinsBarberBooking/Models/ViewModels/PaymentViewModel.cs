namespace MavinsBarberBooking.Models.ViewModels
{
    public class PaymentViewModel
    {
        public string? ServiceName { get; set; }
        public string? BarberName { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal FeeAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string? CheckoutUrl { get; set; }
    }
}