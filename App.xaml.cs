using oculus_sport.Services.Storage;

namespace oculus_sport;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Force Light Theme on Startup
        UserAppTheme = AppTheme.Light;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    protected override async void OnStart()
    {
        var current = Connectivity.NetworkAccess;

        if (current != NetworkAccess.Internet)
        {
            // Offline Mode Logic
            string cachedUser = Preferences.Get("LastUserId", string.Empty);
            if (!string.IsNullOrEmpty(cachedUser))
            {
                await Shell.Current.GoToAsync("//HistoryPage");
            }
        }
    }
}