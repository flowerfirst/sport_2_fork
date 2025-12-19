using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using oculus_sport.Models;
using oculus_sport.Services;
using oculus_sport.Services.Other;
using oculus_sport.ViewModels.Base;

namespace oculus_sport.ViewModels.Main;

[QueryProperty(nameof(Booking), "Booking")]
public partial class BookingConfirmationViewModel : BaseViewModel
{
    private readonly IBookingService _bookingService;
    private readonly NotificationService _notificationService;

    [ObservableProperty]
    private Booking _booking;

    public BookingConfirmationViewModel(IBookingService bookingService, NotificationService notificationService)
    {
        _bookingService = bookingService;
        _notificationService = notificationService;
        Title = "Confirmation";
    }

    [RelayCommand]
    async Task Confirm()
    {
        IsBusy = true;

        // 1. Mark as Confirmed
        Booking.Status = "Confirmed";

        // 2. Save to Database/Service (Calls the merged IBookingService)
        await _bookingService.AddBookingAsync(Booking);

        // 3. Notify user locally (immediate + reminder)
        await _notificationService.NotifyBookingConfirmedAsync(Booking);

        IsBusy = false;

        // 4. Navigate to Success Page
        var navigationParameter = new Dictionary<string, object>
        {
            { "Booking", Booking }
        };
        await Shell.Current.GoToAsync("BookingSuccessPage", navigationParameter);
    }

    [RelayCommand]
    async Task Cancel()
    {
        await Shell.Current.GoToAsync(".."); // Go back
    }
}
