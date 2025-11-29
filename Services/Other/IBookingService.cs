using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using oculus_sport.Models;

namespace oculus_sport.Services
{
    public interface IBookingService
    {
        // --- Core Business Logic (Simulated for In-Memory Test) ---

        /// <summary>
        /// Executes a simulated secure booking process. Checks memory for availability.
        /// </summary>
        Task<Booking?> ProcessAndConfirmBookingAsync(Booking newBooking);

        /// <summary>
        /// Fetches all time slots for a specific facility on a given date and marks them as available/booked.
        /// </summary>
        Task<IEnumerable<TimeSlot>> GetAvailableTimeSlotsAsync(string facilityId, DateTime date);

        /// <summary>
        /// Quick check for availability for UI display.
        /// </summary>
        Task<bool> IsSlotAvailableAsync(string facilityId, string timeSlot, DateTime date);

        /// <summary>
        /// NEW: Calculates the final cost based on facility, time slot, and user details.
        /// </summary>
        Task<string> CalculateFinalCostAsync(string facilityId, string timeSlot, string studentId);

        // --- Data Retrieval and Update ---

        /// <summary>
        /// Fetches all bookings for the current user.
        /// </summary>
        Task<List<Booking>> GetUserBookingsAsync(string userId);

        /// <summary>
        /// Updates a booking status (e.g., Cancellation).
        /// </summary>
        Task<bool> UpdateBookingStatusAsync(Booking booking, string newStatus);

        // --- Frontend Compatibility ---
        // This ensures your BookingViewModel doesn't break. 
        // Implementation will likely just call ProcessAndConfirmBookingAsync.
        Task AddBookingAsync(Booking booking);
    }
}