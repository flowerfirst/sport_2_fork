using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using oculus_sport.Models;
using oculus_sport.Services.Storage;
using oculus_sport.Services.Auth;
using oculus_sport.ViewModels.Base;

namespace oculus_sport.ViewModels.Main
{
    public partial class HomePageViewModel : BaseViewModel
    {
        private readonly FirebaseDataService _dataService;
        private readonly IAuthService _authService; // 2. Add Auth Service field

        [ObservableProperty]
        private string _userName = "Guest";

        [ObservableProperty]
        private ObservableCollection<SportCategory> _categories = new();

        [ObservableProperty]
        private ObservableCollection<Facility> _facilities = new();

        private List<Facility> _allFacilities = new();

        // 3. Update Constructor to accept IAuthService
        public HomePageViewModel(FirebaseDataService dataService, IAuthService authService)
        {
            _dataService = dataService;
            _authService = authService; // Assign it
            Title = "Home";

            // 4. Get the real user name
            var currentUser = _authService.GetCurrentUser();
            if (currentUser != null && !string.IsNullOrEmpty(currentUser.Name))
            {
                UserName = currentUser.Name;
            }

            LoadCategories();
            Task.Run(LoadDataAsync);
        }

        private void LoadCategories()
        {
            Categories.Add(new SportCategory { Name = "Badminton", IsSelected = true });
            Categories.Add(new SportCategory { Name = "Ping-Pong" });
            Categories.Add(new SportCategory { Name = "Basketball" });
        }

        private async Task LoadDataAsync()
        {
            IsBusy = true;

            try
            {
                var fetchedFacilities = await _dataService.GetFacilitiesFromFirestoreAsync();
                _allFacilities.Clear();

                foreach (var facility in fetchedFacilities)
                {
                    if (facility.Name.Contains("Badminton"))
                        facility.LocationMapUrl = "recreation_center.png";
                    else if (facility.Name.Contains("Ping-Pong") || facility.Name.Contains("PingPong"))
                        facility.LocationMapUrl = "recreational_center.png";
                    else if (facility.Name.Contains("Basketball"))
                        facility.LocationMapUrl = "outdoor_court.png";
                    else
                        facility.LocationMapUrl = "recreation_center.png";

                    _allFacilities.Add(facility);
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    FilterFacilities("Badminton");
                });
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Could not load facilities: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        void SelectCategory(SportCategory category)
        {
            if (Categories == null) return;
            foreach (var c in Categories) c.IsSelected = false;
            category.IsSelected = true;
            FilterFacilities(category.Name);
        }

        private void FilterFacilities(string categoryName)
        {
            Facilities.Clear();

            // 1. Define the search term. 
            // If the category is "Ping-Pong", just search for "Ping" to be safe.
            string searchTerm = categoryName;
            if (categoryName == "Ping-Pong")
            {
                searchTerm = "Ping";
            }

            // 2. Perform the filter with the safe term
            var filtered = _allFacilities.Where(f => f.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));

            foreach (var facility in filtered)
            {
                Facilities.Add(facility);
            }
        }

        [RelayCommand]
        async Task BookFacility(Facility facility)
        {
            var navigationParameter = new Dictionary<string, object> { { "Facility", facility } };
            await Shell.Current.GoToAsync("BookingPage", navigationParameter);
        }

        [RelayCommand]
        async Task GoToNotifications()
        {
            await Shell.Current.GoToAsync("NotificationPage");
        }
    }
}