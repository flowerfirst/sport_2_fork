using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using oculus_sport.Services.Auth;
using oculus_sport.ViewModels.Base;

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

        public SignUpPageViewModel(IAuthService authService)
        {
            _authService = authService;
            Title = "Sign Up";
        }

        [RelayCommand]
        async Task SignUp()
        {
            if (IsBusy) return;

            // ... (Validation Logic kept same) ...
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Username) ||
                string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(Name) ||
                string.IsNullOrWhiteSpace(StudentId))
            {
                await Shell.Current.DisplayAlert("Error", "Please fill in all fields.", "OK");
                return;
            }

            if (Password != ConfirmPassword)
            {
                await Shell.Current.DisplayAlert("Error", "Passwords do not match.", "OK");
                return;
            }

            try
            {
                IsBusy = true;
                var newUser = await _authService.SignUpAsync(Email, Password, Name, StudentId, Username);

                if (newUser != null)
                {
                    await Shell.Current.DisplayAlert("Success", "Account created successfully! Please log in.", "OK");

                    // FIX: Use Absolute Route to go to Login
                    await Shell.Current.GoToAsync("//LoginPage");
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
            await Shell.Current.GoToAsync("//LoginPage");
        }
    }
}