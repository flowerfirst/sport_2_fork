using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using oculus_sport.Models;
using System.Diagnostics;

namespace oculus_sport.Services;

public class BookingService : IBookingService
{
    // In-memory list to store bookings while the app is running
    private readonly List<Booking> _bookings = new();

    // Hardcoded prices simulating database lookup
    private readonly Dictionary<string, decimal> _facilityBasePrices = new()
    {
        {"BadmintonCourt", 25.00m}, // RM 25.00
        {"PingPongCourt", 15.00m},  // RM 15.00
        {"BasketballCourt", 30.00m} // RM 30.00
    };

    // -----------------------------------------------------------
    // Core Business Logic (Implements IBookingService contract)
    // -----------------------------------------------------------

    public async Task<string> CalculateFinalCostAsync(string facilityId, string timeSlot, string studentId)
    {
        await Task.Delay(100); // Simulate API call to fetch price

        decimal basePrice;
        // Simple key matching logic or default
        var key = facilityId.Replace(" ", "");
        if (!_facilityBasePrices.TryGetValue(key, out basePrice) && !_facilityBasePrices.TryGetValue("BadmintonCourt", out basePrice)) // Fallback logic
        {
            basePrice = 20.00m;
        }

        decimal finalCost = basePrice;

        if (!string.IsNullOrEmpty(studentId) && studentId.Length > 5)
        {
            finalCost *= 0.9m; // 10% discount
        }

        return $"RM {finalCost:N2}";
    }

    public async Task<Booking?> ProcessAndConfirmBookingAsync(Booking newBooking)
    {
        await Task.Delay(500);

        if (_bookings.Any(b =>
            b.FacilityName == newBooking.FacilityName &&
            b.Date.Date == newBooking.Date.Date &&
            b.TimeSlot == newBooking.TimeSlot))
        {
            Debug.WriteLine("[Simulated Booking Failure] Slot already taken.");
            return null;
        }

        // Calculate Cost if needed
        if (string.IsNullOrEmpty(newBooking.TotalCost) || newBooking.TotalCost == "Free")
            newBooking.TotalCost = await CalculateFinalCostAsync(newBooking.FacilityName, newBooking.TimeSlot, newBooking.ContactStudentId);

        newBooking.Status = "Confirmed";
        newBooking.Date = newBooking.Date.Date;
        _bookings.Add(newBooking);

        return newBooking;
    }

    public async Task<List<Booking>> GetUserBookingsAsync(string userId)
    {
        await Task.Delay(200);
        // Return all bookings for mock purposes if userId matches or is test user
        return _bookings.Where(b => b.UserId == userId || userId == "Tony").OrderBy(b => b.Date).ToList();
    }

    public async Task<IEnumerable<TimeSlot>> GetAvailableTimeSlotsAsync(string facilityId, DateTime date)
    {
        await Task.Delay(100);
        var slots = new List<TimeSlot>();
        for (int hour = 8; hour < 22; hour++)
        {
            var start = new TimeSpan(hour, 0, 0);
            var end = new TimeSpan(hour + 1, 0, 0);
            var range = $"{start:hh\\:mm} - {end:hh\\:mm}";

            bool isBooked = _bookings.Any(b => b.FacilityName == facilityId && b.Date.Date == date.Date && b.TimeSlot == range);

            slots.Add(new TimeSlot
            {
                TimeRange = range, // Use property from your frontend model
                // SlotName = range, // Use if backend model has this
                IsAvailable = !isBooked
            });
        }
        return slots;
    }

    public async Task<bool> IsSlotAvailableAsync(string facilityId, string timeSlot, DateTime date)
    {
        await Task.Delay(50);
        return !_bookings.Any(b =>
            b.FacilityName == facilityId &&
            b.Date.Date == date.Date &&
            b.TimeSlot == timeSlot);
    }

    public Task<bool> UpdateBookingStatusAsync(Booking booking, string newStatus)
    {
        var existing = _bookings.FirstOrDefault(b => b.Id == booking.Id);
        if (existing != null)
        {
            existing.Status = newStatus;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    // --- Frontend Compatibility ---
    public async Task AddBookingAsync(Booking booking)
    {
        await ProcessAndConfirmBookingAsync(booking);
    }
}