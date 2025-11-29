using oculus_sport.ViewModels.Main;

namespace oculus_sport.Views.Main;

public partial class BookingSuccessPage : ContentPage
{
    public BookingSuccessPage(BookingSuccessViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    // Disable hardware back button to prevent going back to "Confirmation"
    protected override bool OnBackButtonPressed()
    {
        return true; // Return true to ignore the back button
    }

}