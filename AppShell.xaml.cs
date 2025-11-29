using oculus_sport.Views.Auth;
using oculus_sport.Views.Main;

namespace oculus_sport;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(BookingPage), typeof(BookingPage));
        Routing.RegisterRoute(nameof(SignUpPage), typeof(SignUpPage));
        Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
        Routing.RegisterRoute(nameof(BookingDetailsPage), typeof(BookingDetailsPage));
        Routing.RegisterRoute(nameof(BookingConfirmationPage), typeof(BookingConfirmationPage));
        Routing.RegisterRoute(nameof(BookingSuccessPage), typeof(BookingSuccessPage));
        Routing.RegisterRoute(nameof(EventDetailsPage), typeof(EventDetailsPage));
        Routing.RegisterRoute(nameof(NotificationPage), typeof(NotificationPage));
    }
}