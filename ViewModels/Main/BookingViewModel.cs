using System.Collections.ObjectModel;
using System.Diagnostics;
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
    private CancellationTokenSource _cts;

    [ObservableProperty]
    private Facility _facility = new();

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Now;

    [ObservableProperty]
    private ObservableCollection<TimeSlot> _timeSlots = new();

    [ObservableProperty]
    private string _availabilityMessage = string.Empty;

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

    private async void StartRealtimeListener()
    {
        var idToken = await SecureStorage.GetAsync("idToken");
        if (string.IsNullOrEmpty(idToken)) return;

        await _bookingService.ListenToBookingsAsync(idToken, (json) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    var docRoot = JsonDocument.Parse(json).RootElement;
                    if (docRoot.TryGetProperty("documents", out var docs))
                    {
                        foreach (var d in docs.EnumerateArray())
                        {
                            var fields = d.GetProperty("fields");
                            var facilityName = fields.GetProperty("facilityName").GetProperty("stringValue").GetString();
                            var timeSlot = fields.GetProperty("timeSlot").GetProperty("stringValue").GetString();

                            var slot = TimeSlots.FirstOrDefault(s => s.TimeRange == timeSlot && s.SlotName.Contains(facilityName));
                            if (slot != null)
                            {
                                slot.IsAvailable = false;
                                if (slot.IsSelected) slot.IsSelected = false; // deselect if already selected
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Listener] JSON parse error: {ex.Message}");
                }
            });
        });
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
            // Combine Firestore bookings + local pending bookings
            var bookedSlots = existingBookings
                .Where(b => b.FacilityName == Facility.FacilityName && b.Date.Date == SelectedDate.Date)
                .Select(b => b.TimeSlot)
                .Concat(_bookingService.LocalPendingBookings
                    .Where(b => b.FacilityName == Facility.FacilityName && b.Date.Date == SelectedDate.Date)
                    .Select(b => b.TimeSlot))
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