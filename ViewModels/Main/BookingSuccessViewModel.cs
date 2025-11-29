using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using oculus_sport.Models;
using oculus_sport.ViewModels.Base;

namespace oculus_sport.ViewModels.Main;

[QueryProperty(nameof(Booking), "Booking")]
public partial class BookingSuccessViewModel : BaseViewModel
{
    [ObservableProperty]
    private Booking _booking;

    public BookingSuccessViewModel()
    {
        Title = "Success";
    }

    [RelayCommand]
    async Task GoHome()
    {
        // Navigate back to the absolute root (Home Page), clearing the navigation stack
        await Shell.Current.GoToAsync("//HomePage");
    }

    [RelayCommand]
    async Task ShareBooking()
    {
        // Placeholder for Share functionality
        if (Booking != null)
            await Shell.Current.DisplayAlert("Share", $"Sharing Booking ID: {Booking.Id}", "OK");
    }
}