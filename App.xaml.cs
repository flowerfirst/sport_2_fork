using Newtonsoft.Json.Linq;
using oculus_sport.Models;
using oculus_sport.Services.Auth;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace oculus_sport;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        UserAppTheme = AppTheme.Light;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window
        {
            Page = new AppShell()
        };

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await HandleStartupAsync();
        });

        return window;
    }

    [SuppressMessage("Interoperability", "CA1416")]
    
    private async Task HandleStartupAsync()
    {
        var timeout = Task.Delay(3000);
        while (Shell.Current == null && !timeout.IsCompleted)
            await Task.Delay(50);

        if (Shell.Current == null)
        {
            Debug.WriteLine("[Startup] Shell.Current still null.");
            return;
        }

        var authService = IPlatformApplication.Current?.Services.GetService<IAuthService>();
        User? cachedUser = null;
        if (authService != null)
        {
            cachedUser = await authService.GetCachedUserAsync();
        }

        var idToken = await SecureStorage.GetAsync("idToken");
        Debug.WriteLine($"[Startup] Retrieved idToken: {(string.IsNullOrEmpty(idToken) ? "null/empty" : "present")}");

        if (!string.IsNullOrEmpty(idToken) && !IsTokenExpired(idToken))
        {
            Debug.WriteLine("[Startup] Token valid. Going home.");
            await Shell.Current.GoToAsync("//HomePage");
            return;
        }

        if (authService != null)
        {
            var refreshedToken = await authService.RefreshIdTokenAsync();
            if (!string.IsNullOrEmpty(refreshedToken))
            {
                await SecureStorage.SetAsync("idToken", refreshedToken);
                await Shell.Current.GoToAsync("//HomePage");
                return;
            }
        }

        if (cachedUser != null)
        {
            Debug.WriteLine("[Startup] No valid token but cached user exists. Navigating in offline mode.");
            await Shell.Current.GoToAsync("//HomePage");
            return;
        }

        await Shell.Current.GoToAsync("//LoginPage");
    }

    [SuppressMessage("Interoperability", "CA1416")]
    private bool IsTokenExpired(string idToken)
    {
        var parts = idToken.Split('.');
        if (parts.Length != 3) return true;

        var payload = parts[1];
        var jsonBytes = Convert.FromBase64String(
            payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=')
        );
        var json = System.Text.Encoding.UTF8.GetString(jsonBytes);

        var expMatch = System.Text.RegularExpressions.Regex.Match(json, "\"exp\":(\\d+)");
        if (!expMatch.Success) return true;

        var expUnix = long.Parse(expMatch.Groups[1].Value);
        var expDate = DateTimeOffset.FromUnixTimeSeconds(expUnix);

        return expDate < DateTimeOffset.UtcNow;
    }
}
