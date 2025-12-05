using oculus_sport.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using oculus_sport.Services.Storage;


namespace oculus_sport.Services;

public class BookingService : IBookingService
{
    // In-memory list to store bookings while the app is running
    private readonly List<Booking> _bookings = new();
    private readonly HttpClient _httpClient;
    private readonly string _projectId = "oculus-sport";
    private readonly string FacilitiesCollection = "facility";
    private readonly FirebaseDataService _firebaseDataService;

    public BookingService(HttpClient httpClient, FirebaseDataService firebaseDataService)
    {
        _httpClient = httpClient;
        _firebaseDataService = firebaseDataService;
    }

    // Hardcoded prices simulating database lookup (should calculate automatically fetch price from firestore)
    //private readonly Dictionary<string, decimal> _facilityBasePrices = new()
    //{
    //    {"BadmintonCourt", 25.00m}, // RM 25.00
    //    {"PingPongCourt", 15.00m},  // RM 15.00
    //    {"BasketballCourt", 30.00m} // RM 30.00
    //};


    // -----------------------------------------------------------
    // Core Business Logic (Implements IBookingService contract)
    // -----------------------------------------------------------

    //------- get price & calculate price
    //---1. get price
    private async Task<decimal> GetFacilityPriceFromFirestoreAsync(string facilityId, string idToken)
    {
        
        var facilities = await _firebaseDataService.GetFacilitiesAsync(idToken);

        // Find the facility by its document ID or name
        var facility = facilities.FirstOrDefault(f => f.FacilityName == facilityId || f.FacilityId == facilityId);

        if (facility == null)
        {
            throw new InvalidOperationException($"Facility {facilityId} not found in Firestore.");
        }

        Debug.WriteLine($"[PRICE] Found facility={facility.FacilityName}, Price={facility.Price}");
        return facility.Price;
    }


    //---2. calc price
    public async Task<string> CalculateFinalCostAsync(string facilityId, string timeSlot, string studentId)
    {
        await Task.Delay(100);

        var idToken = await SecureStorage.GetAsync("idToken");
        if (string.IsNullOrEmpty(idToken))
        {
            throw new InvalidOperationException("No idToken available for Firestore request.");
        }

        Debug.WriteLine($"[BookingService] Starting cost calculation for facility={facilityId}, timeSlot={timeSlot}, studentId={studentId}");

        decimal basePrice = await GetFacilityPriceFromFirestoreAsync(facilityId, idToken);

        decimal finalCost = basePrice;

        if (!string.IsNullOrEmpty(studentId) && studentId.Length > 5)
        {
            Debug.WriteLine($"[PRICE] StudentId eligible for discount. Applying 10% off.");
            finalCost *= 0.9m;
        }
        else
        {
            Debug.WriteLine($"[PRICE] No discount applied.");
        }

        Debug.WriteLine($"[PRICE] Final calculated cost: RM {finalCost:N2}");

        return $"RM {finalCost:N2}";
    }


    //---- fetch available timeslot
    public async Task<IEnumerable<TimeSlot>> GetAvailableTimeSlotsAsync(string facilityId, DateTime date)
    {
        await Task.Delay(100);

        var slots = new List<TimeSlot>();
        var day = date.DayOfWeek;
        List<string> validSlots = new();

        // ----Rules based on facilityId (exact name from Firestore)
        if (facilityId.Contains("Badminton Court"))
        {
            Debug.WriteLine("[BookingService] Badminton rules applied.");
            if (day == DayOfWeek.Monday || day == DayOfWeek.Thursday || day == DayOfWeek.Friday)
            {
                validSlots = new List<string> { "10:00 - 12:00", "12:00 - 14:00", "14:00 - 16:00" };
            }
        }
        else if (facilityId.Contains("Ping-Pong Table"))
        {
            Debug.WriteLine("[BookingService] Ping-Pong rules applied.");
            if (day == DayOfWeek.Monday || day == DayOfWeek.Friday)
            {
                validSlots = new List<string> { "10:00 - 12:00", "12:00 - 14:00", "14:00 - 16:00" };
            }
        }
        else if (facilityId.Contains("Basketball Court"))
        {
            Debug.WriteLine("[BookingService] Basketball rules applied.");
            if (day != DayOfWeek.Saturday && day != DayOfWeek.Sunday)
            {
                validSlots = new List<string> { "10:00 - 12:00", "12:00 - 14:00", "14:00 - 16:00", "16:00 - 18:00" };
            }
        }

        // --- Generate slots only for the selected facility
        foreach (var slot in validSlots)
        {
            bool isBooked = _bookings.Any(b =>
                b.FacilityName == facilityId &&
                b.Date.Date == date.Date &&
                b.TimeSlot == slot);

            Debug.WriteLine($"[BookingService] Slot {facilityId} {slot} booked={isBooked}");

            slots.Add(new TimeSlot
            {
                TimeRange = slot,
                SlotName = $"{facilityId} • {slot}",
                IsAvailable = !isBooked
            });
        }

        Debug.WriteLine($"[BookingService] Returning {slots.Count} slots.");
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
    //--- process user and confirm booking
    public async Task<Booking?> ProcessAndConfirmBookingAsync(Booking newBooking)
    {
        await Task.Delay(500);

        // 1. Check if slot already taken
        if (_bookings.Any(b =>
            b.FacilityName == newBooking.FacilityName &&
            b.Date.Date == newBooking.Date.Date &&
            b.TimeSlot == newBooking.TimeSlot &&
            b.SlotNumber == newBooking.SlotNumber))
        {
            newBooking.Status = "Rejected";
            Debug.WriteLine("[Booking Failure] Slot already taken.");
            return null;
        }

        // 2. Calculate cost
        newBooking.TotalCost = await CalculateFinalCostAsync(
            newBooking.FacilityName,
            newBooking.TimeSlot,
            newBooking.ContactStudentId);

        // 3. Mark as confirmed
        newBooking.Status = "Confirmed";
        newBooking.Date = newBooking.Date.Date;

        // 4. Save in-memory
        _bookings.Add(newBooking);

        // 5. Persist to Firestore
        try
        {
            //--- pass the idToken from CurrentUser or SecureStorage
            var idToken = await SecureStorage.GetAsync("idToken");
            if (!string.IsNullOrEmpty(idToken))
            {
                var saved = await SaveBookingToFirestoreAsync(newBooking, idToken);
                if (!saved)
                {
                    Debug.WriteLine("[BookingService] Firestore save failed.");
                }
                else
                {
                    Debug.WriteLine("[BookingService] Booking saved to Firestore.");
                }
            }
            else
            {
                Debug.WriteLine("[BookingService] No idToken available, skipping Firestore save.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BookingService] Firestore save exception: {ex.Message}");
        }

        return newBooking;

    }


    // ----- save booking info into firestore
    public async Task<bool> SaveBookingToFirestoreAsync(Booking booking, string idToken)
    {
        var url = $"https://firestore.googleapis.com/v1/projects/{_projectId}/databases/(default)/documents/bookings/{booking.Id}";

        var payload = new
        {
            fields = new Dictionary<string, object>
        {
           { "userId", new { stringValue = booking.UserId } },
            { "facilityName", new { stringValue = booking.FacilityName } },
            { "date", new { timestampValue = booking.Date.ToUniversalTime().ToString("o") } },
            { "timeSlot", new { stringValue = booking.TimeSlot } },
            { "slotNumber", new { integerValue = booking.SlotNumber } },
            { "status", new { stringValue = booking.Status } },
            { "totalCost", new { stringValue = booking.TotalCost } },
            { "contactName", new { stringValue = booking.ContactName } },
            { "contactPhone", new { stringValue = booking.ContactPhone } },
            { "contactStudentId", new { stringValue = booking.ContactStudentId } }
        }
        };

        var json = JsonSerializer.Serialize(payload);
        var req = new HttpRequestMessage(HttpMethod.Patch, url) //--use patch to upsert ID
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

        var res = await _httpClient.SendAsync(req);
        var responseBody = await res.Content.ReadAsStringAsync();

        Debug.WriteLine($"[Firestore SaveBooking] Status={res.StatusCode}, Response={responseBody}");

        return res.IsSuccessStatusCode;
    }


    public async Task<List<Booking>> GetUserBookingsAsync(string userId)
    {
        var idToken = await SecureStorage.GetAsync("idToken");
        var url = $"https://firestore.googleapis.com/v1/projects/{_projectId}/databases/(default)/documents/bookings";
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

        var res = await _httpClient.SendAsync(req);
        var responseBody = await res.Content.ReadAsStringAsync();
        Debug.WriteLine($"[Firestore GetUserBookings] Response status={res.StatusCode}");

        var bookings = new List<Booking>();

        if (res.IsSuccessStatusCode)
        {
            var json = JsonDocument.Parse(responseBody);
            foreach (var doc in json.RootElement.GetProperty("documents").EnumerateArray())
            {
                var fields = doc.GetProperty("fields");
                var booking = new Booking
                {
                    Id = doc.GetProperty("name").GetString()?.Split('/').Last(),
                    UserId = fields.GetProperty("userId").GetProperty("stringValue").GetString(),
                    FacilityName = fields.GetProperty("facilityName").GetProperty("stringValue").GetString(),
                    Date = DateTime.Parse(fields.GetProperty("date").GetProperty("timestampValue").GetString(), null, DateTimeStyles.RoundtripKind),
                    TimeSlot = fields.GetProperty("timeSlot").GetProperty("stringValue").GetString(),
                    SlotNumber = int.Parse(fields.GetProperty("slotNumber").GetProperty("integerValue").GetString()),
                    Status = fields.GetProperty("status").GetProperty("stringValue").GetString(),
                    TotalCost = fields.GetProperty("totalCost").GetProperty("stringValue").GetString(),
                    ContactName = fields.GetProperty("contactName").GetProperty("stringValue").GetString(),
                    ContactPhone = fields.GetProperty("contactPhone").GetProperty("stringValue").GetString(),
                    ContactStudentId = fields.GetProperty("contactStudentId").GetProperty("stringValue").GetString()
                };

                if (booking.UserId == userId)
                    bookings.Add(booking);
            }
        }

        Debug.WriteLine($"[Firestore GetUserBookings] Found {bookings.Count} bookings for userId={userId}");
        return bookings.OrderBy(b => b.Date).ToList();
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