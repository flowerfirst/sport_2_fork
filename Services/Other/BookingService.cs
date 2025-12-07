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

    private readonly List<Booking> _bookings = new();
    private readonly List<Booking> _localPendingBookings = new();

    public IReadOnlyList<Booking> LocalPendingBookings => _localPendingBookings.AsReadOnly();

    private readonly HttpClient _httpClient;
    private readonly string _projectId = "oculus-sport";
    //private readonly string FacilitiesCollection = "facility";
    private readonly FirebaseDataService _firebaseDataService;

    public BookingService(HttpClient httpClient, FirebaseDataService firebaseDataService)
    {
        _httpClient = httpClient;
        _firebaseDataService = firebaseDataService;
    }


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
        var slots = new List<TimeSlot>();
        List<string> validSlots = GetValidSlotsByFacility(facilityId, date.DayOfWeek);

        var existingBookings = await GetBookingsByFacilityAndDateAsync(facilityId, date);
        var bookedSlots = existingBookings.Select(b => b.TimeSlot)
                    .Concat(_localPendingBookings
                            .Where(b => b.FacilityName == facilityId && b.Date.Date == date.Date)
                            .Select(b => b.TimeSlot))
                    .ToHashSet();

        foreach (var slot in validSlots)
        {
            slots.Add(new TimeSlot
            {
                TimeRange = slot,
                SlotName = $"{facilityId} • {slot}",
                IsAvailable = !bookedSlots.Contains(slot)
            });

            Debug.WriteLine($"[BookingService] Slot {facilityId} {slot} available={!bookedSlots.Contains(slot)}");
        }

        return slots;
    }


    private List<string> GetValidSlotsByFacility(string facilityId, DayOfWeek day)
    {
        if (facilityId.Contains("Badminton Court") &&
            (day == DayOfWeek.Monday || day == DayOfWeek.Thursday || day == DayOfWeek.Friday))
            return new List<string> { "10:00 - 12:00", "12:00 - 14:00", "14:00 - 16:00" };

        if (facilityId.Contains("Ping-Pong Table") &&
            (day == DayOfWeek.Monday || day == DayOfWeek.Friday))
            return new List<string> { "10:00 - 12:00", "12:00 - 14:00", "14:00 - 16:00" };

        if (facilityId.Contains("Basketball Court") &&
            day != DayOfWeek.Saturday && day != DayOfWeek.Sunday)
            return new List<string> { "10:00 - 12:00", "12:00 - 14:00", "14:00 - 16:00", "16:00 - 18:00" };

        return new List<string>();
    }

    public async Task<bool> IsSlotAvailableAsync(string facilityId, string timeSlot, DateTime date)
    {
        var existingBookings = await GetBookingsByFacilityAndDateAsync(facilityId, date);
        return !existingBookings.Any(b => b.TimeSlot == timeSlot);
    }

    //--- process user and confirm booking
    public async Task<Booking?> ProcessAndConfirmBookingAsync(Booking newBooking)
    {
        // Check availability
        if (!await IsSlotAvailableAsync(newBooking.FacilityName, newBooking.TimeSlot, newBooking.Date))
        {
            newBooking.Status = "Rejected";
            Debug.WriteLine("[BookingService] Slot already taken.");
            return newBooking;
        }

        // Calculate cost
        newBooking.TotalCost = await CalculateFinalCostAsync(
            newBooking.FacilityName,
            newBooking.TimeSlot,
            newBooking.ContactStudentId);

        // Confirm booking
        newBooking.Status = "Confirmed";
        newBooking.Date = newBooking.Date.Date;

        // Add to local pending bookings
        _localPendingBookings.Add(newBooking);

        // Persist to Firestore
        var idToken = await SecureStorage.GetAsync("idToken");
        if (!string.IsNullOrEmpty(idToken))
        {
            try
            {
                var saved = await SaveBookingToFirestoreAsync(newBooking, idToken);
                Debug.WriteLine(saved ? "[BookingService] Booking saved to Firestore." : "[BookingService] Firestore save failed.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BookingService] Firestore save exception: {ex.Message}");
            }
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
            
            // fetch img for history
            { "imageUrl", new { stringValue = booking.FacilityImage } },
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
                    FacilityImage = fields.GetProperty("imageUrl").GetProperty("stringValue").GetString(), // ✅ new line
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

    // -------------------------
    // Firestore Polling Listener (Pseudo Real-Time)
    // -------------------------
    public async Task<List<Booking>> GetBookingsByFacilityAndDateAsync(string facilityName, DateTime date)
    {
        var idToken = await SecureStorage.GetAsync("idToken");
        if (string.IsNullOrEmpty(idToken)) return new List<Booking>();

        var url = $"https://firestore.googleapis.com/v1/projects/{_projectId}/databases/(default)/documents/bookings";
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

        var res = await _httpClient.SendAsync(req);
        var responseBody = await res.Content.ReadAsStringAsync();
        var bookings = new List<Booking>();

        if (!res.IsSuccessStatusCode) return bookings;

        var json = JsonDocument.Parse(responseBody);
        if (!json.RootElement.TryGetProperty("documents", out var docs)) return bookings;

        foreach (var doc in docs.EnumerateArray())
        {
            var fields = doc.GetProperty("fields");
            var booking = new Booking
            {
                FacilityName = fields.GetProperty("facilityName").GetProperty("stringValue").GetString(),
                TimeSlot = fields.GetProperty("timeSlot").GetProperty("stringValue").GetString(),
                Date = DateTime.Parse(fields.GetProperty("date").GetProperty("timestampValue").GetString(), null, DateTimeStyles.RoundtripKind)
            };

            if (booking.FacilityName == facilityName && booking.Date.Date == date.Date)
                bookings.Add(booking);
        }

        // Merge with local pending bookings
        bookings.AddRange(_localPendingBookings
            .Where(b => b.FacilityName == facilityName && b.Date.Date == date.Date));

        return bookings;
    }

    public async Task ListenToBookingsAsync(string idToken, Action<string> onUpdate)
    {
        if (string.IsNullOrEmpty(idToken)) return;

        var url = $"https://firestore.googleapis.com/v1/projects/{_projectId}/databases/(default)/documents/bookings";
        int pollIntervalSeconds = 5;

        while (true)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

                var res = await _httpClient.SendAsync(req);
                var responseBody = await res.Content.ReadAsStringAsync();

                if (res.IsSuccessStatusCode)
                    onUpdate(responseBody);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Listen] Exception during polling: {ex.Message}");
            }

            await Task.Delay(pollIntervalSeconds * 1000);
        }
    }



    // --- Frontend Compatibility ---
    public async Task AddBookingAsync(Booking booking)
    {
        await ProcessAndConfirmBookingAsync(booking);
    }
}