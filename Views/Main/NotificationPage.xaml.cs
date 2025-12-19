using oculus_sport.ViewModels.Main;

namespace oculus_sport.Views.Main;

public partial class NotificationPage : ContentPage
{
    private readonly NotificationPageViewModel _viewModel;

    public NotificationPage(NotificationPageViewModel vm)
    {
        InitializeComponent();
        BindingContext = _viewModel = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }
}