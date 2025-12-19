using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using oculus_sport.Services;
using oculus_sport.Services.Auth;
using oculus_sport.Services.Other;
using oculus_sport.Services.Storage;
using oculus_sport.ViewModels.Auth;
using oculus_sport.ViewModels.Main;
using oculus_sport.Views.Auth;
using oculus_sport.Views.Main;

using System.IO;
using System.Text;

using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Hosting;
using Microsoft.Extensions.DependencyInjection;


namespace oculus_sport;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });



        // ---------------------------------------------------------
        // ❗ FIREBASE INIT (Commented out for stability)
        //    The platform-specific setup (MainActivity.cs / AppDelegate.cs)
        //    must be completed by the backend engineer before this is uncommented.
        // ---------------------------------------------------------
        // CrossFirebase.Initialize(); 
        // ---------------------------------------------------------


        // 1. Services

        builder.Services.AddSingleton<HttpClient>();
        builder.Services.AddSingleton<IAuthService, FirebaseAuthService>();
        builder.Services.AddSingleton<FirebaseDataService>();


        builder.Services.AddSingleton<Services.Other.ConnectivityService>();
        builder.Services.AddSingleton<IBookingService, BookingService>();
        builder.Services.AddSingleton<LocalDataService>();
        builder.Services.AddSingleton<NotificationStore>();
        builder.Services.AddSingleton<NotificationService>();


        // 2. ViewModels
        builder.Services.AddTransient<LoginPageViewModel>();
        builder.Services.AddTransient<SignUpPageViewModel>();
        builder.Services.AddTransient<HomePageViewModel>();
        builder.Services.AddTransient<SchedulePageViewModel>();
        builder.Services.AddTransient<EventPageViewModel>();
        builder.Services.AddTransient<ProfilePageViewModel>();
        builder.Services.AddTransient<HistoryPageViewModel>();
        builder.Services.AddTransient<PriceListViewModel>();

        // Booking Flow
        builder.Services.AddTransient<BookingViewModel>();
        builder.Services.AddTransient<BookingDetailsViewModel>();
        builder.Services.AddTransient<BookingConfirmationViewModel>();
        builder.Services.AddTransient<BookingSuccessViewModel>();

        // Events & Notifications (Your additions)
        builder.Services.AddTransient<EventDetailsViewModel>();
        builder.Services.AddTransient<NotificationPageViewModel>();

        // 3. Views
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<SignUpPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<SchedulePage>();
        builder.Services.AddTransient<EventPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<HistoryPage>();
        builder.Services.AddTransient<PriceListPage>();


        // Booking Flow Pages
        builder.Services.AddTransient<BookingPage>();
        builder.Services.AddTransient<BookingDetailsPage>();
        builder.Services.AddTransient<BookingConfirmationPage>();
        builder.Services.AddTransient<BookingSuccessPage>();

        // Events & Notifications (Your additions)
        builder.Services.AddTransient<EventDetailsPage>();
        builder.Services.AddTransient<NotificationPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // inside CreateMauiApp(), before return builder.Build();
        var logPath = Path.Combine(FileSystem.AppDataDirectory, "crash_report.txt");

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            File.AppendAllText(logPath, $"UNHANDLED: {DateTime.UtcNow}\n{ex}\n\n", Encoding.UTF8);
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            File.AppendAllText(logPath, $"UNOBSERVED TASK: {DateTime.UtcNow}\n{e.Exception}\n\n", Encoding.UTF8);
            e.SetObserved();
        };

#if ANDROID
        Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (sender, args) =>
        {
            File.AppendAllText(logPath, $"ANDROID UNHANDLED: {DateTime.UtcNow}\n{args.Exception}\n\n", Encoding.UTF8);
            // optional: args.Handled = true;
        };
#endif

        return builder.Build();
    }
}