using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using oculus_sport.Services.Auth;
using oculus_sport.ViewModels.Base;
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
        private string _password = string.Empty;

        [ObservableProperty]
        private string _confirmPassword = string.Empty;

        // Added properties required for Backend Signup
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _studentId = string.Empty;

        public SignUpPageViewModel(IAuthService authService)
        {
            _authService = authService;
            Title = "Sign Up";
        }

        [RelayCommand]
        async Task SignUp()
        {
            if (IsBusy) return;

            // 1. Basic Validation
            if (string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Password) ||
                string.IsNullOrWhiteSpace(ConfirmPassword) ||
                string.IsNullOrWhiteSpace(Name) ||
                string.IsNullOrWhiteSpace(StudentId))
            {
                await Shell.Current.DisplayAlert("Error", "Please fill in all fields (Email, Password, Name, ID).", "OK");
                return;
            }

            if (Password != ConfirmPassword)
            {
                await Shell.Current.DisplayAlert("Error", "Passwords do not match.", "OK");
                return;
            }

            // 2. Strong Password Validation
            if (!IsStrongPassword(Password))
            {
                await Shell.Current.DisplayAlert("Weak Password",
                    "Password must be at least 8 characters long, contain an uppercase letter, and a special character.",
                    "OK");
                return;
            }

            try
            {
                IsBusy = true;

                // Call Auth Service with all required backend parameters
                var newUser = await _authService.SignUpAsync(Email, Password, Name, StudentId);

                if (newUser != null)
                {
                    await Shell.Current.DisplayAlert("Success", "Account created successfully! Please log in.", "OK");
                    // Redirect to Login Page
                    await Shell.Current.GoToAsync("..");
                }
            }
            catch (Exception ex)
            {
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
            await Shell.Current.GoToAsync("..");
        }

        private bool IsStrongPassword(string password)
        {
            // Regex: At least 8 chars, 1 Upper, 1 Special char
            var regex = new Regex(@"^(?=.*[A-Z])(?=.*[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]).{8,}$");
            return regex.IsMatch(password);
        }
    }
}