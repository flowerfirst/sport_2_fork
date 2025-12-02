using oculus_sport.ViewModels.Main;

namespace oculus_sport.Views.Main;

public partial class ProfilePage : ContentPage
{
    private readonly ProfilePageViewModel _vm;

    public ProfilePage(ProfilePageViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}
