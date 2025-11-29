using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using oculus_sport.Models;
using oculus_sport.Services;
using oculus_sport.Services.Storage;
using oculus_sport.ViewModels.Base;

namespace oculus_sport.ViewModels.Main;

public partial class HistoryPageViewModel : BaseViewModel
{
    private readonly IBookingService _firebaseService;
    private readonly LocalDatabaseService _localService;
    private readonly Services.Other.ConnectivityService _connectivity;

    [ObservableProperty]
    private ObservableCollection<Booking> _myBookings = new();

    [ObservableProperty]
    private bool _hasNoBookings;

    public HistoryPageViewModel(IBookingService firebase, LocalDatabaseService local, Services.Other.ConnectivityService conn)
    {
        _firebaseService = firebase;
        _localService = local;
        _connectivity = conn;
        Title = "Booking History";
    }

    [RelayCommand]
    async Task LoadBookings()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            List<Booking> bookings;

            if (_connectivity.IsConnected())
            {
                // ONLINE: Fetch from Firebase (mock service for now)
                bookings = await _firebaseService.GetUserBookingsAsync("Tony");

                // Cache locally
                await _localService.SaveBookingsAsync(bookings);
            }
            else
            {
                // OFFLINE: Fetch from SQLite
                bookings = await _localService.GetBookingsAsync();

                if (bookings.Count > 0)
                    await Shell.Current.DisplayAlert("Offline Mode", "Showing cached history.", "OK");
            }

            MyBookings.Clear();
            foreach (var b in bookings)
            {
                MyBookings.Add(b);
            }

            HasNoBookings = MyBookings.Count == 0;
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to load bookings: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    async Task ViewBookingDetails(Booking booking)
    {
        if (booking == null) return;

        var navigationParameter = new Dictionary<string, object>
        {
            { "Booking", booking }
        };

        await Shell.Current.GoToAsync("BookingSuccessPage", navigationParameter);
    }
}