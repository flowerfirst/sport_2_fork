using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using oculus_sport.Services.Auth;
using oculus_sport.ViewModels.Base;
using System.Diagnostics;

namespace oculus_sport.ViewModels.Auth
{
    public partial class SignUpPageViewModel : BaseViewModel
    {
        private readonly IAuthService _authService;

        [ObservableProperty]
        private string _email = string.Empty;

        // Restored: Username property (from your backup)
        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private string _confirmPassword = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _studentId = string.Empty;

        [ObservableProperty]
        private string _phoneNumber = string.Empty;

        // NOTE: We replaced PhoneNumber with Username based on your preference
        // If you need both, you can just add PhoneNumber back here.

        public SignUpPageViewModel(IAuthService authService)
        {
            _authService = authService;
            Title = "Sign Up";
        }

        [RelayCommand]
        async Task SignUp()
        {
            if (IsBusy)
            {
                Debug.WriteLine("[SignUp] Operation blocked: already busy.");
                return;
            }

            // 1. Basic Validation
            if (string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Password) ||
                string.IsNullOrWhiteSpace(ConfirmPassword) ||
                string.IsNullOrWhiteSpace(Name) ||
                string.IsNullOrWhiteSpace(StudentId) ||
                string.IsNullOrWhiteSpace(PhoneNumber) ||
                string.IsNullOrWhiteSpace(Username))
            {
                Debug.WriteLine("[SignUp] Validation failed: missing required fields.");
                await Shell.Current.DisplayAlert("Error", "Please fill in all fields (Email, Username, Name, ID).", "OK");
                return;
            }

            if (Password != ConfirmPassword)
            {
                Debug.WriteLine("[SignUp] Validation failed: passwords do not match.");
                await Shell.Current.DisplayAlert("Error", "Passwords do not match.", "OK");
                return;
            }

            try
            {
                IsBusy = true;
                Debug.WriteLine($"[SignUp] Starting signup for Email={Email}, Username={Username}");

                // Updated: Call Auth Service with Username
                // Ensure IAuthService.SignUpAsync signature matches this call!
                var newUser = await _authService.SignUpAsync(Email, Password, Name, PhoneNumber, StudentId, Username);

                if (newUser != null)
                {
                    Debug.WriteLine($"[SignUp] Signup successful. UserId={newUser.Id}");

                    await Shell.Current.DisplayAlert("Success", "Account created successfully! Please log in.", "OK");

                    // Navigate to Login Page (Absolute Route)
                    await Shell.Current.GoToAsync("//LoginPage");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SignUp] Exception: {ex.Message}");
                await Shell.Current.DisplayAlert("Error", $"Sign up failed: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        async Task GoToLogin()
        {
            // Navigate back to Login Page
            await Shell.Current.GoToAsync("//LoginPage");
        }
    }
}