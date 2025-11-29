using Foundation;
using Plugin.Firebase.Core.Platforms.iOS;   // CrossFirebase.Initialize()
using UIKit;

namespace oculus_sport;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        // Initialize native Firebase iOS SDK
        Firebase.Core.App.Configure();

        // Initialize the Plugin.Firebase cross-platform wrapper
        CrossFirebase.Initialize();

        return base.FinishedLaunching(application, launchOptions);
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}