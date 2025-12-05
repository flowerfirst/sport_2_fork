using oculus_sport.Services.Auth;
using oculus_sport.Services.Storage;
using System.Diagnostics;

namespace oculus_sport;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Force Light Theme on Startup
        UserAppTheme = AppTheme.Light;

        // Set AppShell as the root page
        MainPage = new AppShell();

        // Defer startup logic until Shell is ready
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await HandleStartupAsync();
        });
    }

    private async Task HandleStartupAsync()
    {
        // Guard against Shell not being ready
        if (Shell.Current == null)
        {
            Debug.WriteLine("[Startup] Shell.Current is null, skipping startup logic.");
            return;
        }

        var current = Connectivity.NetworkAccess;
        Debug.WriteLine($"[Startup] Network access: {current}");

        if (current != NetworkAccess.Internet)
        {
            string cachedUser = Preferences.Get("LastUserId", string.Empty);
            if (!string.IsNullOrEmpty(cachedUser))
            {
                await Shell.Current.GoToAsync("//HistoryPage");
                return;
            }
        }

        var idToken = await SecureStorage.GetAsync("idToken");
        Debug.WriteLine($"[Startup] Retrieved idToken: {(string.IsNullOrEmpty(idToken) ? "null/empty" : "present")}");

        if (!string.IsNullOrEmpty(idToken))
        {
            bool expired = IsTokenExpired(idToken);
            Debug.WriteLine($"[Startup] idToken expired? {expired}");

            if (!expired)
            {
                await Shell.Current.GoToAsync("//HomePage");
                return;
            }
        }

        var refreshToken = await SecureStorage.GetAsync("refreshToken");
        Debug.WriteLine($"[Startup] Retrieved refreshToken: {(string.IsNullOrEmpty(refreshToken) ? "null/empty" : "present")}");

        var authService = IPlatformApplication.Current.Services.GetService<IAuthService>();

        if (!string.IsNullOrEmpty(refreshToken) && authService != null)
        {
            Debug.WriteLine("[Startup] Attempting to refresh idToken...");
            var newIdToken = await authService.RefreshIdTokenAsync();

            if (!string.IsNullOrEmpty(newIdToken))
            {
                Debug.WriteLine("[Startup] Refresh succeeded, new idToken stored.");
                await SecureStorage.SetAsync("idToken", newIdToken);
                await Shell.Current.GoToAsync("//HomePage");
                return;
            }
            else
            {
                Debug.WriteLine("[Startup] Refresh failed, navigating to login.");
            }
        }
        else
        {
            Debug.WriteLine("[Startup] No refreshToken or authService unavailable.");
        }

        // Only if refresh fails → go to login
        await Shell.Current.GoToAsync("//LoginPage");
    }


    // Helper to check if token expired
    private bool IsTokenExpired(string idToken)
    {
        var parts = idToken.Split('.');
        if (parts.Length != 3)
        {
            Debug.WriteLine("[TokenCheck] Invalid JWT format.");
            return true;
        }

        var payload = parts[1];
        var jsonBytes = Convert.FromBase64String(
            payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=')
        );
        var json = System.Text.Encoding.UTF8.GetString(jsonBytes);

        var expMatch = System.Text.RegularExpressions.Regex.Match(json, "\"exp\":(\\d+)");
        if (!expMatch.Success)
        {
            Debug.WriteLine("[TokenCheck] No exp claim found.");
            return true;
        }

        var expUnix = long.Parse(expMatch.Groups[1].Value);
        var expDate = DateTimeOffset.FromUnixTimeSeconds(expUnix);

        Debug.WriteLine($"[TokenCheck] Token expires at: {expDate:yyyy-MM-dd HH:mm:ss} UTC");
        Debug.WriteLine($"[TokenCheck] Current time: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");

        bool expired = expDate < DateTimeOffset.UtcNow;
        Debug.WriteLine($"[TokenCheck] Token expired? {expired}");

        return expired;
    }

}
