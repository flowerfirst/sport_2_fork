using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using oculus_sport.Models;
using oculus_sport.Services.Auth;
using oculus_sport.Services.Storage;
using oculus_sport.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace oculus_sport.ViewModels.Main
{
    // This allows the Login Page to pass the "User" object directly to Home
    [QueryProperty(nameof(CurrentUser), "User")]
    public partial class HomePageViewModel : BaseViewModel
    {
        // ----------------- Services -----------------
        private readonly FirebaseDataService _dataService;
        private readonly IAuthService _authService;

        // ----------------- Fields -----------------
        private string? _idToken;
        private List<Facility> _allFacilities = new();

        // ----------------- Observable Properties -----------------
        [ObservableProperty]
        private ObservableCollection<SportCategory> _categories = new();

        [ObservableProperty]
        private ObservableCollection<Facility> _facilities = new();

        [ObservableProperty]
        private string _userName = "Guest";

        [ObservableProperty]
        private User _currentUser;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        // ----------------- Constructor -----------------
        public HomePageViewModel(FirebaseDataService dataService, IAuthService authService)
        {
            _dataService = dataService;
            _authService = authService;
            Title = "Home";

            // 1. Initial User Load (Try to get from AuthService memory first)
            // This handles the case where the app is just opening and user is already logged in
            var cachedUser = _authService.GetCurrentUser();
            if (cachedUser != null)
            {
                CurrentUser = cachedUser;
                UserName = cachedUser.Name;
            }

            LoadCategories();

            // Kick off data loading immediately
            Task.Run(LoadDataAsync);
        }

        // ----------------- User Sync (Critical for Login) -----------------

        // This method is called automatically when the [QueryProperty] "User" is set
        // (i.e., when navigating from Login Page -> Home Page)
        partial void OnCurrentUserChanged(User value)
        {
            if (value != null)
            {
                Debug.WriteLine($"[HomeViewModel] User Changed: {value.Name}");
                UserName = value.Name;

                // If the user object passed in has a fresh token, use it
                if (!string.IsNullOrEmpty(value.IdToken))
                {
                    _idToken = value.IdToken;
                }

                // Reload data for this specific user
                _ = LoadDataAsync();
            }
        }

        // Helper to sync user state manually if needed (e.g. from App.xaml.cs startup)
        public async Task UserHomepageSync(string uid, string idToken)
        {
            _idToken = idToken;
            var user = await _dataService.GetUserFromFirestore(uid, idToken);
            if (user != null)
            {
                user.IdToken = idToken;
                CurrentUser = user; // Triggers OnCurrentUserChanged
            }
        }

        // ----------------- Page Lifecycle (Called by HomePage.xaml.cs) -----------------

        public async Task LoadAsync()
        {
            // 1. Try to get/refresh the token
            var idToken = await SecureStorage.GetAsync("idToken");
            if (string.IsNullOrEmpty(idToken) || IsTokenExpired(idToken))
            {
                var refreshed = await _authService.RefreshIdTokenAsync();
                if (!string.IsNullOrEmpty(refreshed))
                {
                    idToken = refreshed;
                }
            }

            if (!string.IsNullOrEmpty(idToken))
            {
                _idToken = idToken;
                StatusMessage = string.Empty;
            }
            else
            {
                StatusMessage = "Offline mode: limited data available.";
            }

            // 2. Ensure we have a user in memory (pull from cache if needed)
            var user = _authService.GetCurrentUser() ?? await _authService.GetCachedUserAsync();
            if (user != null)
            {
                UserName = user.Name;
                if (!string.IsNullOrEmpty(idToken))
                {
                    user.IdToken = idToken;
                }
                CurrentUser = user;
            }
            else if (!string.IsNullOrEmpty(_idToken))
            {
                // Fallback: fetch user from DB using stored ID when we have a token
                var uid = Preferences.Get("LastUserId", "");
                if (!string.IsNullOrEmpty(uid))
                {
                    await UserHomepageSync(uid, _idToken);
                }
            }
            else
            {
                StatusMessage = "Session expired. Please log in again.";
                await Shell.Current.GoToAsync("//LoginPage");
                return;
            }

            // 3. Only attempt remote data load when a valid token exists
            if (!string.IsNullOrEmpty(_idToken) && _allFacilities.Count == 0)
            {
                await LoadDataAsync();
            }
        }

        // ----------------- Data Loading (Merged with Map Logic) -----------------

        private void LoadCategories()
        {
            Categories.Clear();
            Categories.Add(new SportCategory { Name = "Badminton", IsSelected = true });
            Categories.Add(new SportCategory { Name = "Ping-Pong" });
            Categories.Add(new SportCategory { Name = "Basketball" });
        }

        private async Task LoadDataAsync()
        {
            if (IsBusy) return;

            // Double check token availability
            if (string.IsNullOrEmpty(_idToken)) _idToken = await SecureStorage.GetAsync("idToken");
            if (string.IsNullOrEmpty(_idToken)) return;

            IsBusy = true;

            try
            {
                // Fetch from Firestore using the ID Token
                var fetchedFacilities = await _dataService.GetFacilitiesAsync(_idToken);

                _allFacilities.Clear();

                // Process and Assign Local Maps (Your Map Logic)
                foreach (var facility in fetchedFacilities)
                {
                    // Logic to assign the correct local map image based on name
                    if (facility.FacilityName.Contains("Badminton", StringComparison.OrdinalIgnoreCase))
                        facility.LocationMapUrl = "recreation_center.png";
                    else if (facility.FacilityName.Contains("Ping", StringComparison.OrdinalIgnoreCase))
                        facility.LocationMapUrl = "recreation_center.png";
                    else if (facility.FacilityName.Contains("Basketball", StringComparison.OrdinalIgnoreCase))
                        facility.LocationMapUrl = "outdoor_court.png";
                    else
                        facility.LocationMapUrl = "recreation_center.png"; // Fallback

                    _allFacilities.Add(facility);
                }

                // Update UI on Main Thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    FilterFacilities("Badminton");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Error] LoadDataAsync: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ----------------- Filtering & Interaction -----------------

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

            // Handle naming mismatch (Ping-Pong vs Ping)
            string searchTerm = categoryName;
            if (categoryName.Contains("Ping")) searchTerm = "Ping";

            var filtered = _allFacilities
                .Where(f => f.Category.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                         || f.FacilityName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));

            foreach (var facility in filtered)
            {
                Facilities.Add(facility);
            }
        }

        [RelayCommand]
        async Task BookFacility(Facility facility)
        {
            Debug.WriteLine($"[DEBUG] Selected Facility: {facility.FacilityName}");
            var navigationParameter = new Dictionary<string, object> { { "Facility", facility } };
            await Shell.Current.GoToAsync("BookingPage", navigationParameter);
        }

        [RelayCommand]
        async Task GoToNotifications()
        {
            await Shell.Current.GoToAsync("NotificationPage");
        }

        // ----------------- Token Helper -----------------

        private bool IsTokenExpired(string idToken)
        {
            try
            {
                var parts = idToken.Split('.');
                if (parts.Length != 3) return true;

                var payload = parts[1];
                var payloadPad = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                var jsonBytes = Convert.FromBase64String(payloadPad);
                var json = System.Text.Encoding.UTF8.GetString(jsonBytes);

                var expMatch = System.Text.RegularExpressions.Regex.Match(json, "\"exp\":(\\d+)");
                if (!expMatch.Success) return true;

                var expUnix = long.Parse(expMatch.Groups[1].Value);
                var expDate = DateTimeOffset.FromUnixTimeSeconds(expUnix);

                return expDate < DateTimeOffset.UtcNow;
            }
            catch
            {
                return true;
            }
        }
    }
}