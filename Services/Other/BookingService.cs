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
        var bookedSlots = existingBookings
            .Where(b => BlocksSlot(b.Status))
            .Select(b => b.TimeSlot)
            .Concat(_localPendingBookings
                    .Where(b => b.FacilityName == facilityId && b.Date.Date == date.Date && BlocksSlot(b.Status))
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
            if (json.RootElement.TryGetProperty("documents", out var docs))
            {
                foreach (var doc in docs.EnumerateArray())
                {
                    try
                    {
                        var fields = doc.GetProperty("fields");

                        var booking = new Booking
                        {
                            Id = TryGetDocumentId(doc),
                            UserId = GetStringField(fields, "userId"),
                            FacilityName = GetStringField(fields, "facilityName"),
                            FacilityImage = GetStringField(fields, "imageUrl"), // ✅ new line
                            Date = GetDateField(fields, "date"),
                            TimeSlot = GetStringField(fields, "timeSlot"),
                            SlotNumber = GetIntField(fields, "slotNumber"),
                            Status = GetStringField(fields, "status"),
                            TotalCost = GetStringField(fields, "totalCost"),
                            ContactName = GetStringField(fields, "contactName"),
                            ContactPhone = GetStringField(fields, "contactPhone"),
                            ContactStudentId = GetStringField(fields, "contactStudentId")
                        };

                        if (string.IsNullOrEmpty(userId) || booking.UserId == userId)
                            bookings.Add(booking);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Firestore GetUserBookings] Skipping doc due to parse error: {ex.Message}");
                    }
                }
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
            try
            {
                var fields = doc.GetProperty("fields");
                var booking = new Booking
                {
                    FacilityName = GetStringField(fields, "facilityName"),
                    TimeSlot = GetStringField(fields, "timeSlot"),
                    Date = GetDateField(fields, "date"),
                    Status = GetStringField(fields, "status")
                };

                if (booking.FacilityName == facilityName && booking.Date.Date == date.Date && BlocksSlot(booking.Status))
                    bookings.Add(booking);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GetBookingsByFacilityAndDateAsync] Skipping doc due to parse error: {ex.Message}");
            }
        }

        bookings.AddRange(_localPendingBookings
            .Where(b => b.FacilityName == facilityName && b.Date.Date == date.Date && BlocksSlot(b.Status)));

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

    // --------- Helper methods for resilient JSON parsing ---------
    private static string TryGetDocumentId(JsonElement doc)
    {
        if (doc.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
        {
            var name = nameProp.GetString();
            if (!string.IsNullOrEmpty(name))
            {
                var parts = name.Split('/');
                return parts.Last();
            }
        }
        return string.Empty;
    }

    private static string GetStringField(JsonElement fields, string fieldName)
    {
        if (fields.TryGetProperty(fieldName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Object)
            {
                if (prop.TryGetProperty("stringValue", out var sv) && sv.ValueKind == JsonValueKind.String)
                    return sv.GetString() ?? string.Empty;

                if (prop.TryGetProperty("integerValue", out var iv) && iv.ValueKind == JsonValueKind.String)
                    return iv.GetString() ?? string.Empty;

                if (prop.TryGetProperty("timestampValue", out var tv) && tv.ValueKind == JsonValueKind.String)
                    return tv.GetString() ?? string.Empty;
            }

            if (prop.ValueKind == JsonValueKind.String)
                return prop.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private static int GetIntField(JsonElement fields, string fieldName)
    {
        if (fields.TryGetProperty(fieldName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Object && prop.TryGetProperty("integerValue", out var iv))
            {
                var s = iv.GetString();
                if (int.TryParse(s, out var v)) return v;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var num))
                return num;

            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var sval))
                return sval;
        }

        return 0;
    }

    private static DateTime GetDateField(JsonElement fields, string fieldName)
    {
        if (fields.TryGetProperty(fieldName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Object && prop.TryGetProperty("timestampValue", out var tv))
            {
                var s = tv.GetString();
                if (DateTime.TryParse(s, null, DateTimeStyles.RoundtripKind, out var dt))
                    return dt;
            }

            if (prop.ValueKind == JsonValueKind.Object && prop.TryGetProperty("stringValue", out var sv))
            {
                var s = sv.GetString();
                if (DateTime.TryParse(s, out var dt2)) return dt2;
            }

            if (prop.ValueKind == JsonValueKind.String)
            {
                var s = prop.GetString();
                if (DateTime.TryParse(s, out var dt3)) return dt3;
            }
        }

        return DateTime.MinValue;
    }

    private static bool BlocksSlot(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return true;

        return status.Equals("Confirmed", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("Pending", StringComparison.OrdinalIgnoreCase);
    }
}