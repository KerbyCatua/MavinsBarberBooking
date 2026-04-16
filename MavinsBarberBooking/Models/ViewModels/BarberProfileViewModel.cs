using System;
using System.Collections.Generic;

namespace MavinsBarberBooking.Models.ViewModels
{
    public class BarberProfileViewModel
    {
        public int BarberId { get; set; }
        public string Name { get; set; }
        public string Rank { get; set; }
        public string ProfileImage { get; set; }
        public string AvailabilityStatus { get; set; }

        public IEnumerable<BarberServiceViewModel> Services { get; set; } = new List<BarberServiceViewModel>();
        public IEnumerable<BarberTimeSlotViewModel> TimeSlots { get; set; } = new List<BarberTimeSlotViewModel>();
    }

    public class BarberServiceViewModel
    {
        public int ServiceId { get; set; }
        public string Name { get; set; }
        public int DurationMinutes { get; set; }
        public decimal Price { get; set; }
        public string Details { get; set; }
        public string ServiceImage { get; set; }
    }

    public class BarberTimeSlotViewModel
    {
        public int SlotId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
}