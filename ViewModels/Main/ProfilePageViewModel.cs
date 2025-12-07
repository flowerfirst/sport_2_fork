using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using oculus_sport.Services.Auth;
using oculus_sport.ViewModels.Base;
using Microsoft.Maui.Controls;

namespace oculus_sport.ViewModels.Main;

public partial class ProfilePageViewModel : BaseViewModel
{
    private readonly IAuthService _authService;

    // Remove the default values
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _studentId = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private bool _isDarkMode;

    public ProfilePageViewModel(IAuthService authService)
    {
        _authService = authService;
        Title = "My Profile";

        IsDarkMode = Application.Current.UserAppTheme == AppTheme.Dark;

        // LOAD REAL USER DATA
        LoadUserData();
    }

    private void LoadUserData()
    {
        // Get the user from the service
        var currentUser = _authService.GetCurrentUser();

        if (currentUser != null)
        {
            Name = currentUser.Name;
            StudentId = currentUser.StudentId;
            Email = currentUser.Email;
        }
        else
        {
            // Fallback if something went wrong
            Name = "Guest";
            Email = "Not Logged In";
        }
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
            await _authService.LogoutAsync();

            // UPDATED: Navigate to LoginPage usually makes more sense for Logout
            // But //SignUpPage will also work now that we fixed AppShell.xaml.cs
            await Shell.Current.GoToAsync("//LoginPage");
        }
    }
}