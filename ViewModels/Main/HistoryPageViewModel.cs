using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using oculus_sport.Models;
using oculus_sport.Services;
using oculus_sport.Services.Storage;
using oculus_sport.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Diagnostics;

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
        if (IsBusy)
        {
            Debug.WriteLine("[History] LoadBookings blocked: already busy.");
            return;
        }
        IsBusy = true;
        Debug.WriteLine("[History] Starting LoadBookings...");

        try
        {
            List<Booking> bookings;
            var userId = Preferences.Get("LastUserId", string.Empty);
            Debug.WriteLine($"[History] Current userId={userId}");

            if (_connectivity.IsConnected())
            {
                Debug.WriteLine("[History] Online mode: fetching from Firebase...");
                bookings = await _firebaseService.GetUserBookingsAsync(userId);
                Debug.WriteLine($"[History] Firebase returned {bookings.Count} bookings.");

                await _localService.SaveBookingsAsync(bookings);
                Debug.WriteLine("[History] Cached bookings locally.");
            }
            else
            {
                Debug.WriteLine("[History] Offline mode: fetching from SQLite...");
                bookings = await _localService.GetBookingsAsync();
                Debug.WriteLine($"[History] SQLite returned {bookings.Count} bookings.");

                if (bookings.Count > 0)
                    await Shell.Current.DisplayAlert("Offline Mode", "Showing cached history.", "OK");
            }

            MyBookings.Clear();
            foreach (var b in bookings)
            {
                Debug.WriteLine($"[History] Adding booking {b.Id}, Facility={b.FacilityName}, Date={b.Date}, Status={b.Status}");
                MyBookings.Add(b);
            }

            HasNoBookings = MyBookings.Count == 0;
            Debug.WriteLine($"[History] HasNoBookings={HasNoBookings}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[History] Exception: {ex.Message}");
            await Shell.Current.DisplayAlert("Error", $"Failed to load bookings: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
            Debug.WriteLine("[History] LoadBookings finished. IsBusy reset.");
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

    public void OnAppearing()
    {
        LoadBookingsCommand.Execute(null);
    }
}