using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
//using static System.Net.Mime.MediaTypeNames;
using Microsoft.Maui.Controls;
using oculus_sport.Services.Auth;
using oculus_sport.ViewModels.Base;
using System.Diagnostics;

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
                var result = await _authService.LoginAsync(Email, Password);

                // ---- sync username in homepage w login
                if (result != null)
                {
                    Debug.WriteLine($"[DEBUG Login] IdToken from auth: {result.IdToken}");

                    // --- save token
                    await SecureStorage.SetAsync("idToken", result.IdToken);
                    if (!string.IsNullOrEmpty(result.RefreshToken))
                        await SecureStorage.SetAsync("refreshToken", result.RefreshToken);
                   
                    Preferences.Set("LastUserId", result.Id);

                    // --- nav to homepage
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
            await Shell.Current.GoToAsync("//SignUpPage");

        }
    }
}