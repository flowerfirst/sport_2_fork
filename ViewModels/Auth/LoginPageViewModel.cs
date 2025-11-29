using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using oculus_sport.Services.Auth;
using oculus_sport.ViewModels.Base;
using static System.Net.Mime.MediaTypeNames;

namespace oculus_sport.ViewModels.Auth
{
    public partial class LoginPageViewModel : BaseViewModel
    {
        private readonly IAuthService _authService;

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        public LoginPageViewModel(IAuthService authService)
        {
            _authService = authService;
            Title = "Login";
        }

        [RelayCommand]
        async Task Login()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;

                // Call the actual auth service (now returns User object)
                var result = await _authService.LoginAsync(Email, Password);

                if (result != null)
                {
                    // Success! Navigate to Main Tabs (HomePage) using absolute routing
                    await Shell.Current.GoToAsync($"//{nameof(Views.Main.HomePage)}");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Login failed: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        async Task GoToSignUp()
        {
            // Navigate to Sign Up Page
            await Shell.Current.GoToAsync(nameof(Views.Auth.SignUpPage));
        }
    }
}