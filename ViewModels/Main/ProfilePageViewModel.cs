using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using oculus_sport.Services.Auth;
using oculus_sport.ViewModels.Base;
using static System.Net.Mime.MediaTypeNames;

namespace oculus_sport.ViewModels.Main;

public partial class ProfilePageViewModel : BaseViewModel
{
    private readonly IAuthService _authService;

    [ObservableProperty]
    private string _name = "Tony Choo";

    [ObservableProperty]
    private string _studentId = "BCS23020003";

    [ObservableProperty]
    private string _email = "tony@student.uts.edu.my";

    [ObservableProperty]
    private bool _isDarkMode;

    public ProfilePageViewModel(IAuthService authService)
    {
        _authService = authService;
        Title = "My Profile";

        // Check current theme
        IsDarkMode = Application.Current.UserAppTheme == AppTheme.Dark;
    }

    partial void OnIsDarkModeChanged(bool value)
    {
        Application.Current.UserAppTheme = value ? AppTheme.Dark : AppTheme.Light;
    }

    [RelayCommand]
    async Task Logout()
    {
        bool confirm = await Shell.Current.DisplayAlert("Logout", "Are you sure you want to log out?", "Yes", "No");
        if (confirm)
        {
            _authService.Logout();
            // Navigate to Sign Up Page (Absolute Route to clear stack)
            await Shell.Current.GoToAsync("//SignUpPage");
        }
    }
}