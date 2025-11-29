using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using oculus_sport.Models;
using oculus_sport.Services;
using oculus_sport.Services.Auth; // Added for future user check
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

    // Updated constructor to accept both services
    public BookingViewModel(IBookingService bookingService, IAuthService authService)
    {
        _bookingService = bookingService;
        _authService = authService;
        Title = "Select Time";
    }

    partial void OnFacilityChanged(Facility value) => GenerateTimeSlots();
    async partial void OnSelectedDateChanged(DateTime value)
    {
        IsBusy = true;
        await Task.Delay(300);
        GenerateTimeSlots();
        IsBusy = false;
    }

    private void GenerateTimeSlots()
    {
        TimeSlots.Clear();
        AvailabilityMessage = string.Empty;

        var day = SelectedDate.DayOfWeek;
        bool isOpen = false;
        List<string> validSlots = new();

        // 1. Check Rules based on Facility Name (Your Allocation Logic)
        if (Facility.Name.Contains("Badminton"))
        {
            if (day == DayOfWeek.Monday || day == DayOfWeek.Thursday || day == DayOfWeek.Friday)
            {
                isOpen = true;
                validSlots = new List<string> { "10:00 - 12:00", "12:00 - 14:00", "14:00 - 16:00" };
            }
            else
            {
                AvailabilityMessage = "Badminton is only available on Mon, Thu, and Fri.";
            }
        }
        else if (Facility.Name.Contains("Ping-Pong"))
        {
            if (day == DayOfWeek.Monday || day == DayOfWeek.Friday)
            {
                isOpen = true;
                validSlots = new List<string> { "10:00 - 12:00", "12:00 - 14:00", "14:00 - 16:00" };
            }
            else
            {
                AvailabilityMessage = "Ping-Pong is only available on Mon and Fri.";
            }
        }
        else if (Facility.Name.Contains("Basketball"))
        {
            if (day != DayOfWeek.Saturday && day != DayOfWeek.Sunday)
            {
                isOpen = true;
                validSlots = new List<string> { "10:00 - 12:00", "12:00 - 14:00", "14:00 - 16:00", "16:00 - 18:00" };
            }
            else
            {
                AvailabilityMessage = "Basketball is closed on weekends.";
            }
        }

        // 2. Populate Slots if Open
        if (isOpen)
        {
            foreach (var slot in validSlots)
            {
                TimeSlots.Add(new TimeSlot { TimeRange = slot, IsAvailable = true });
            }
        }
    }

    [RelayCommand]
    void SelectSlot(TimeSlot slot)
    {
        if (slot == null) return;
        foreach (var s in TimeSlots) s.IsSelected = false;
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

        // Create Draft Booking
        var draftBooking = new Booking
        {
            // Use Auth Service to get ID if available, else placeholder
            UserId = _authService.GetCurrentUser()?.Id ?? "Tony",
            FacilityName = Facility.Name,
            FacilityImage = Facility.ImageUrl,
            Location = Facility.Location,
            Date = SelectedDate,
            TimeSlot = selectedSlot.TimeRange,
            Status = "Draft"
        };

        var navigationParameter = new Dictionary<string, object>
        {
            { "Booking", draftBooking }
        };

        await Shell.Current.GoToAsync("BookingDetailsPage", navigationParameter);
    }
}