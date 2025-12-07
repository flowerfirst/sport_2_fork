using System;
using SQLite; // Required for SQLite attributes

namespace oculus_sport.Models
{
    public class Booking
    {
        // [PrimaryKey] ensures the local booking record can be looked up quickly.
        [PrimaryKey]
        public string Id { get; set; } = Guid.NewGuid().ToString().Substring(0, 8).ToUpper();

        // Use [Indexed] for efficient querying in SQLite (e.g., finding all of a user's bookings)
        [Indexed]
        public string UserId { get; set; } = string.Empty; // Initialized for .NET 9 safety

        // Facility Details
        public string FacilityName { get; set; } = string.Empty;
        public string FacilityImage { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;

        // Time Details
        public DateTime Date { get; set; }
        public string TimeSlot { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public int SlotNumber { get; set; }

        // Contact Details (Required for your Booking Form)
        public string ContactName { get; set; } = string.Empty;
        public string ContactStudentId { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;

        // Payment (Retained your 'Free' default but kept the field)
        public decimal Price { get; set; } = 0m;
        public string TotalCost { get; set; }
    }
}