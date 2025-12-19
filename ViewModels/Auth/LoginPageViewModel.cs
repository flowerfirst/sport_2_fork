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

            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                await Shell.Current.DisplayAlert("Error", "Please enter both Email and Password.", "OK");
                return;
            }

            try
            {
                IsBusy = true;

                // The AuthService now handles the logic for username vs email lookup internally
                // We just pass the input (which could be email OR username)
                var result = await _authService.LoginAsync(Email, Password);
                var token = await SecureStorage.GetAsync("idToken");
                Debug.WriteLine($"[DEBUG] Stored id token: {token}");
                if (result != null)
                {
                    Debug.WriteLine($"[DEBUG Login] Login successful. User Name: {result.Name}");
                    Debug.WriteLine($"[DEBUG Login] IdToken from auth: {result.IdToken}");

                    // --- save token logic is handled inside AuthService now, but we can double check or keep this if needed
                    // (Ideally, AuthService should handle persistence, but keeping it here for safety if your AuthService doesn't)
                    await SecureStorage.SetAsync("idToken", result.IdToken);
                    if (!string.IsNullOrEmpty(result.RefreshToken))
                        await SecureStorage.SetAsync("refreshToken", result.RefreshToken);

                    Preferences.Set("LastUserId", result.Id);

                    // --- nav to homepage AND PASS USER OBJECT to update "Hello, Name" immediately
                    await Shell.Current.GoToAsync($"//{nameof(Views.Main.HomePage)}",
                        new Dictionary<string, object> { { "User", result } });
                }

            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Login failed: {ex.Message}", "OK");
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