using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using oculus_sport.Models;
using oculus_sport.Services;
using oculus_sport.ViewModels.Base;

namespace oculus_sport.ViewModels.Main;

[QueryProperty(nameof(Booking), "Booking")]
public partial class BookingConfirmationViewModel : BaseViewModel
{
    private readonly IBookingService _bookingService;

    [ObservableProperty]
    private Booking _booking;

    public BookingConfirmationViewModel(IBookingService bookingService)
    {
        _bookingService = bookingService;
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

        IsBusy = false;

        // 3. Navigate to Success Page
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
