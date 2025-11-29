using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using oculus_sport.Models;
using oculus_sport.ViewModels.Base;

namespace oculus_sport.ViewModels.Main;

[QueryProperty(nameof(Booking), "Booking")]
public partial class BookingDetailsViewModel : BaseViewModel
{
    [ObservableProperty]
    private Booking _booking = new();

    // Bindable properties for the form
    [ObservableProperty]
    private string _contactName = string.Empty;

    [ObservableProperty]
    private string _contactStudentId = string.Empty;

    [ObservableProperty]
    private string _contactPhone = string.Empty;

    public BookingDetailsViewModel()
    {
        Title = "Booking Details";
    }

    [RelayCommand]
    async Task Proceed()
    {
        if (string.IsNullOrWhiteSpace(ContactName) ||
            string.IsNullOrWhiteSpace(ContactStudentId) ||
            string.IsNullOrWhiteSpace(ContactPhone))
        {
            await Shell.Current.DisplayAlert("Error", "Please fill in all details", "OK");
            return;
        }

        // Update the booking object with input data
        Booking.ContactName = ContactName;
        Booking.ContactStudentId = ContactStudentId;
        Booking.ContactPhone = ContactPhone;

        // Pass to Confirmation Page
        var navigationParameter = new Dictionary<string, object>
        {
            { "Booking", Booking }
        };

        await Shell.Current.GoToAsync("BookingConfirmationPage", navigationParameter);
    }
}