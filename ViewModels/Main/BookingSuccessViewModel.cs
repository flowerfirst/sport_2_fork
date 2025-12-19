using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using oculus_sport.Models;
using oculus_sport.ViewModels.Base;

namespace oculus_sport.ViewModels.Main;

[QueryProperty(nameof(Booking), "Booking")]
[QueryProperty(nameof(IsFromHistory), "IsFromHistory")]
public partial class BookingSuccessViewModel : BaseViewModel
{
    [ObservableProperty]
    private Booking _booking;

    [ObservableProperty]
    private bool _isFromHistory;

    public BookingSuccessViewModel()
    {
        Title = "Success";
    }

    [RelayCommand]
    async Task GoHome()
    {
        if (IsFromHistory)
        {
            await Shell.Current.GoToAsync("..");

            await Shell.Current.GoToAsync("//HomePage");
        }
        else
        {
            await Shell.Current.GoToAsync("//HomePage");
        }
    }

    [RelayCommand]
    async Task ShareBooking()
    {
        if (Booking != null)
            await Shell.Current.DisplayAlert("Share", $"Sharing Booking ID: {Booking.Id}", "OK");
    }
}
