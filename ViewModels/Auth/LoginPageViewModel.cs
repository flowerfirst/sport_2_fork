using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using oculus_sport.Services.Auth;
using oculus_sport.ViewModels.Base;
//using static System.Net.Mime.MediaTypeNames;
using Microsoft.Maui.Controls;

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

            //------------- Basic Validation
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                await Application.Current.MainPage.DisplayAlert("Error", "Please enter both Email and Password.", "OK");
                return;
            }

            try
            {
                IsBusy = true;

                // Call the actual auth service (returns User object)
                var result = await _authService.LoginAsync(Email, Password);

                // ---- sync username in homepage w login
                if (result != null)
                {
                    await Shell.Current.GoToAsync($"//{nameof(Views.Main.HomePage)}", 
                        new Dictionary<string, object>{{"User", result }});
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