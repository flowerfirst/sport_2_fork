using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json.Linq;
using oculus_sport.Models;
using oculus_sport.Services.Storage;
using oculus_sport.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Diagnostics;
using oculus_sport.Services.Auth;

namespace oculus_sport.ViewModels.Main
{
    [QueryProperty(nameof(CurrentUser), "User")]
    public partial class HomePageViewModel : BaseViewModel
    {
        //------------ services
        private readonly FirebaseDataService _firebaseService;
        private readonly IAuthService _authService;
        private string? _idToken;
        private readonly ObservableCollection<Facility> _allFacilities = new();

        //------------- observable properties
        [ObservableProperty] private ObservableCollection<SportCategory> _categories = new();
        [ObservableProperty] private ObservableCollection<Facility> _facilities = new();
        [ObservableProperty] private string userName;
        [ObservableProperty] private User currentUser;
        [ObservableProperty] private string statusMessage = string.Empty;


        partial void OnCurrentUserChanged(User value)
        {
            if (value != null)
            {
                UserName = value.Name;
                _idToken = value.IdToken;

                // Fire and forget async call
                _ = ValidateFacilities();
                _ = LoadDataAsync();
            }
        }

        private async Task ValidateFacilities()
        {
            Debug.WriteLine($"[DEBUG] Using idToken in ValidateFacilities: {_idToken}");
            await _firebaseService.ValidateFacilityCollectionAsync(_idToken);
        }


        public HomePageViewModel(FirebaseDataService firebaseService, IAuthService authService)
        {
            _firebaseService = firebaseService;
            _authService = authService;
            Title = "Home";

            // Initialize categories
            Categories.Add(new SportCategory { Name = "Badminton", IsSelected = true });
            Categories.Add(new SportCategory { Name = "Ping-Pong" });
            Categories.Add(new SportCategory { Name = "Basketball" });
        }


        // --------- sycn homepage user name with login
        public async Task UserHomepageSync(string uid, string idToken)
        {
            _idToken = idToken;

            var user = await _firebaseService.GetUserFromFirestore(uid, idToken);
            if (user != null)
            {
                user.IdToken = idToken;
                CurrentUser = user;
            }
        }


        [RelayCommand]
        public void SelectCategory(SportCategory category)
        {
            if (Categories == null) return;

            foreach (var c in Categories) c.IsSelected = false;
            category.IsSelected = true;

            FilterFacilities(category.Name);
        }
        private async Task LoadDataAsync()
        {
            Categories.Clear();
            Categories.Add(new SportCategory { Name = "Badminton", IsSelected = true });
            Categories.Add(new SportCategory { Name = "Ping-Pong" });
            Categories.Add(new SportCategory { Name = "Basketball" });

            _allFacilities.Clear();

            var facilities = await _firebaseService.GetFacilitiesAsync(_idToken);
            foreach (var f in facilities)
                _allFacilities.Add(f);

            FilterFacilities("Badminton");
        }
        private void FilterFacilities(string categoryName)
        {
            Facilities.Clear();

            var filtered = _allFacilities
                .Where(f => f.Category.Equals(categoryName, StringComparison.OrdinalIgnoreCase));

            foreach (var facility in filtered)
                Facilities.Add(facility);
        }


        [RelayCommand]
        async Task BookFacility(Facility facility)
        {
            Debug.WriteLine($"[DEBUG BOOKINGPAGE] Selected Facility: {facility.FacilityId}, {facility.FacilityName}, {facility.Location}, {facility.Price}");
            var navigationParameter = new Dictionary<string, object> { { "Facility", facility } };
            await Shell.Current.GoToAsync("BookingPage", navigationParameter);
        }

        [RelayCommand]
        async Task GoToNotifications()
        {
            await Shell.Current.GoToAsync("NotificationPage");
        }

        public async Task LoadAsync()
        {
            var user = _authService.GetCurrentUser();
            if (user == null)
            {
                StatusMessage = "No logged in user detected. Please go to Login Page.";
                await Shell.Current.GoToAsync("//Auth/LoginPage");
                return;
            }

            // Check token validity
            var idToken = await SecureStorage.GetAsync("idToken");
            if (string.IsNullOrEmpty(idToken) || IsTokenExpired(idToken))
            {
                StatusMessage = "Session expired. Please log in again.";
                await Shell.Current.GoToAsync("//Auth/LoginPage");
                return;
            }

            // If valid, refresh and continue
            StatusMessage = $"Welcome back, {user.Name}";
            await _authService.RefreshIdTokenAsync();
        }


        private bool IsTokenExpired(string idToken)
        {
            var parts = idToken.Split('.');
            if (parts.Length != 3) return true;

            var payload = parts[1];
            var jsonBytes = Convert.FromBase64String(
                payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=')
            );
            var json = System.Text.Encoding.UTF8.GetString(jsonBytes);

            var expMatch = System.Text.RegularExpressions.Regex.Match(json, "\"exp\":(\\d+)");
            if (!expMatch.Success) return true;

            var expUnix = long.Parse(expMatch.Groups[1].Value);
            var expDate = DateTimeOffset.FromUnixTimeSeconds(expUnix);

            return expDate < DateTimeOffset.UtcNow;
        }

    }
}