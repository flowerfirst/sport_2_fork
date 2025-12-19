using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using oculus_sport.Models;
using oculus_sport.Services;
using oculus_sport.Services.Auth;
using System.Text.Json;


using oculus_sport.ViewModels.Base;

namespace oculus_sport.ViewModels.Main;

[QueryProperty(nameof(Facility), "Facility")]
public partial class BookingViewModel : BaseViewModel
{
    private readonly IBookingService _bookingService;
    private readonly IAuthService _authService; // Kept from backend changes

    [ObservableProperty]
    private Facility _facility = new();

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Now;

    [ObservableProperty]
    private ObservableCollection<TimeSlot> _timeSlots = new();

    [ObservableProperty]
    private string _availabilityMessage = string.Empty;

    private bool _isRealtimeListenerActive;

    // Updated constructor to accept both services
    public BookingViewModel(IBookingService bookingService, IAuthService authService)
    {
        _bookingService = bookingService;
        _authService = authService;
        Title = "Select Time";
    }

    // --- onchange when usee tapped on the facility grid
    partial void OnFacilityChanged(Facility value)
    {
        Debug.WriteLine($"[DEBUG CHECK ONCHANGE] Facility received: {Facility.FacilityName}, {Facility.Location}, RM {Facility.Price}, Rating {Facility.Rating}");
        GenerateTimeSlots();
        StartRealtimeListener();
    }

    async partial void OnSelectedDateChanged(DateTime value)
    {
        IsBusy = true;
        await Task.Delay(300);
        GenerateTimeSlots();
        StartRealtimeListener();
        IsBusy = false;
    }

    private void StartRealtimeListener()
    {
        if (_isRealtimeListenerActive)
        {
            return;
        }

        _isRealtimeListenerActive = true;
        _ = ListenForBookingUpdatesAsync();
    }

    private async Task ListenForBookingUpdatesAsync()
    {
        var idToken = await SecureStorage.GetAsync("idToken");
        if (string.IsNullOrEmpty(idToken))
        {
            _isRealtimeListenerActive = false;
            return;
        }

        await _bookingService.ListenToBookingsAsync(idToken, HandleBookingUpdate);
    }

    private void HandleBookingUpdate(string json)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("documents", out var docs)) return;

                foreach (var d in docs.EnumerateArray())
                {
                    if (!d.TryGetProperty("fields", out var fields)) continue;
                    var facilityName = GetStringField(fields, "facilityName");
                    if (!string.Equals(facilityName, Facility?.FacilityName, StringComparison.OrdinalIgnoreCase)) continue;

                    var bookingDate = GetDateField(fields, "date");
                    if (bookingDate == null || bookingDate.Value.Date != SelectedDate.Date) continue;

                    var timeSlot = GetStringField(fields, "timeSlot");
                    if (string.IsNullOrWhiteSpace(timeSlot)) continue;

                    var slot = TimeSlots.FirstOrDefault(s => s.TimeRange.Equals(timeSlot, StringComparison.OrdinalIgnoreCase));
                    if (slot == null) continue;

                    slot.IsAvailable = false;
                    if (slot.IsSelected) slot.IsSelected = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Listener] JSON parse error: {ex.Message}");
            }
        });
    }

    private static string GetStringField(JsonElement fields, string fieldName)
    {
        if (fields.TryGetProperty(fieldName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Object && prop.TryGetProperty("stringValue", out var sv))
            {
                return sv.GetString() ?? string.Empty;
            }

            if (prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static DateTime? GetDateField(JsonElement fields, string fieldName)
    {
        if (!fields.TryGetProperty(fieldName, out var prop)) return null;

        if (prop.ValueKind == JsonValueKind.Object)
        {
            if (prop.TryGetProperty("timestampValue", out var tv) &&
                tv.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(tv.GetString(), null, DateTimeStyles.RoundtripKind, out var timestampDate))
            {
                return timestampDate;
            }

            if (prop.TryGetProperty("stringValue", out var sv) &&
                DateTime.TryParse(sv.GetString(), out var stringDate))
            {
                return stringDate;
            }
        }
        else if (prop.ValueKind == JsonValueKind.String &&
                 DateTime.TryParse(prop.GetString(), out var directDate))
        {
            return directDate;
        }

        return null;
    }




    public async void GenerateTimeSlots()
    {
        Debug.WriteLine($"[GenerateTimeSlots] Facility={Facility.FacilityName}, Date={SelectedDate:yyyy-MM-dd}");

        TimeSlots.Clear();
        AvailabilityMessage = string.Empty;

        var day = SelectedDate.DayOfWeek;
        bool isOpen = false;
        List<string> validSlots = new();

        // Rules based on category
        if (Facility.Category.Equals("Badminton", StringComparison.OrdinalIgnoreCase))
        {
            if (day == DayOfWeek.Monday || day == DayOfWeek.Thursday || day == DayOfWeek.Friday)
            {
                isOpen = true;
                validSlots = new List<string> { "10:00 - 12:00", "12:00 - 14:00", "14:00 - 16:00" };
            }
            else AvailabilityMessage = "Badminton is only available on Mon, Thu, and Fri.";
        }
        else if (Facility.Category.Equals("Ping-Pong", StringComparison.OrdinalIgnoreCase))
        {
            if (day == DayOfWeek.Monday || day == DayOfWeek.Friday)
            {
                isOpen = true;
                validSlots = new List<string> { "10:00 - 12:00", "12:00 - 14:00", "14:00 - 16:00" };
            }
            else AvailabilityMessage = "Ping-Pong is only available on Mon and Fri.";
        }
        else if (Facility.Category.Equals("Basketball", StringComparison.OrdinalIgnoreCase))
        {
            if (day != DayOfWeek.Saturday && day != DayOfWeek.Sunday)
            {
                isOpen = true;
                validSlots = new List<string> { "10:00 - 12:00", "12:00 - 14:00", "14:00 - 16:00", "16:00 - 18:00" };
            }
            else AvailabilityMessage = "Basketball is closed on weekends.";
        }

        if (!isOpen) return;

        // Create initial slots
        foreach (var slot in validSlots)
        {
            TimeSlots.Add(new TimeSlot
            {
                TimeRange = slot,
                SlotName = $"{Facility.FacilityName} • {slot}",
                IsAvailable = true
            });
        }

        try
        {
            // Fetch existing bookings from Firestore
            var existingBookings = await _bookingService.GetUserBookingsAsync("");
            var activeBookings = existingBookings
                .Where(b => b.FacilityName == Facility.FacilityName &&
                            b.Date.Date == SelectedDate.Date &&
                            BlocksSlotStatus(b.Status));

            var blockingPending = _bookingService.LocalPendingBookings
                .Where(b => b.FacilityName == Facility.FacilityName &&
                            b.Date.Date == SelectedDate.Date &&
                            BlocksSlotStatus(b.Status));

            var bookedSlots = activeBookings
                .Select(b => b.TimeSlot)
                .Concat(blockingPending.Select(b => b.TimeSlot))
                .ToHashSet();

            // Mark unavailable
            foreach (var slot in TimeSlots)
            {
                if (bookedSlots.Contains(slot.TimeRange))
                {
                    slot.IsAvailable = false;
                    if (slot.IsSelected) slot.IsSelected = false; // deselect if needed
                    Debug.WriteLine($"[GenerateTimeSlots] Slot {slot.SlotName} marked unavailable");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GenerateTimeSlots] Error fetching bookings: {ex.Message}");
        }
    }

    private static bool BlocksSlotStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return true;

        return status.Equals("Confirmed", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("Pending", StringComparison.OrdinalIgnoreCase);
    }



    [RelayCommand]
    void SelectSlot(TimeSlot slot)
    {
        if (slot == null) return;

        // Check availability in real-time
        if (!slot.IsAvailable)
        {
            Shell.Current.DisplayAlert("Oops!", "Slot is booked. Choose another slot.", "OK");
            return;
        }

        // Deselect all
        foreach (var s in TimeSlots) s.IsSelected = false;

        // Select tapped slot
        slot.IsSelected = true;
    }



    [RelayCommand]
    async Task ConfirmBooking()
    {
        var selectedSlot = TimeSlots.FirstOrDefault(s => s.IsSelected);
        if (selectedSlot == null)
        {
            string msg = string.IsNullOrEmpty(AvailabilityMessage) ? "Please select a time slot." : AvailabilityMessage;
            await Shell.Current.DisplayAlert("Unavailable", msg, "OK");
            return;
        }

        if (!selectedSlot.IsAvailable)
        {
            await Shell.Current.DisplayAlert("Oops!", "Slot is booked. Choose another slot.", "OK");
            return;
        }

        // Create Draft Booking
        var currentUser = _authService.GetCurrentUser();
        if (currentUser == null || string.IsNullOrEmpty(currentUser.Id))
        {
            throw new InvalidOperationException("No authenticated user found. Please log in before booking.");
        }

        var draftBooking = new Booking
        {
            UserId = currentUser.Id,
            FacilityName = Facility.FacilityName,
            FacilityImage = Facility.ImageUrl,
            Location = Facility.Location,
            Date = SelectedDate,
            TimeSlot = selectedSlot.TimeRange,
            Status = "Pending" //---auto
        };


        var navigationParameter = new Dictionary<string, object>
        {
            { "Booking", draftBooking }
        };

        await Shell.Current.GoToAsync("BookingDetailsPage", navigationParameter);
    }
}