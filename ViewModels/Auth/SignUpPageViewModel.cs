using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using oculus_sport.Services.Auth;
using oculus_sport.ViewModels.Base;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace oculus_sport.ViewModels.Auth
{
    public partial class SignUpPageViewModel : BaseViewModel
    {
        private readonly IAuthService _authService;

        [ObservableProperty]
        private string _email = string.Empty;

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
                string.IsNullOrWhiteSpace(PhoneNumber)
                )
            {
                Debug.WriteLine("[SignUp] Validation failed: missing required fields.");
                await Shell.Current.DisplayAlert("Error", "Please fill in all fields (Email, Password, Name, ID).", "OK");
                return;
            }

            if (Password != ConfirmPassword)
            {
                Debug.WriteLine("[SignUp] Validation failed: passwords do not match.");
                await Shell.Current.DisplayAlert("Error", "Passwords do not match.", "OK");
                return;
            }

            // 2. Strong Password Validation (optional)
            //if (!IsStrongPassword(Password))
            //{
            //    Debug.WriteLine("[SignUp] Validation failed: weak password.");
            //    await Shell.Current.DisplayAlert("Weak Password",
            //        "Password must be at least 8 characters long, contain an uppercase letter, and a special character.",
            //        "OK");
            //    return;
            //}

            try
            {
                IsBusy = true;
                Debug.WriteLine($"[SignUp] Starting signup for Email={Email}, Name={Name}, StudentId={StudentId}");

                // Call Auth Service with all required backend parameters
                var newUser = await _authService.SignUpAsync(Email, Password, Name, StudentId, PhoneNumber);

                if (newUser != null)
                {
                    Debug.WriteLine($"[SignUp] Signup successful. UserId={newUser.Id}, Email={newUser.Email}, Phone={PhoneNumber}");

                    //// Save token for persistence
                    //await SecureStorage.SetAsync("idToken", newUser.IdToken);
                    //if (!string.IsNullOrEmpty(newUser.RefreshToken))
                    //{
                    //    await SecureStorage.SetAsync("refreshToken", newUser.RefreshToken);
                    //    Debug.WriteLine("[SignUp] RefreshToken saved.");
                    //}

                    // Navigate to LoginPage (or HomePage if auto-login desired)
                    Debug.WriteLine("[SignUp] Navigating to LoginPage...");
                    await Shell.Current.GoToAsync("//LoginPage");
                }
                else
                {
                    Debug.WriteLine("[SignUp] Signup returned null user object.");
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
                Debug.WriteLine("[SignUp] Operation finished. IsBusy reset to false.");
            }
        }

        [RelayCommand]
        async Task GoToLogin()
        {
            // Navigate back to Login Page
            await Shell.Current.GoToAsync("//LoginPage");


            //await Shell.Current.GoToAsync("..");

        }
    }
}