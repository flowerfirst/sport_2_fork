using oculus_sport.ViewModels.Main;
using oculus_sport.Models;

namespace oculus_sport.Views.Main;

public partial class HomePage : ContentPage
{
    private readonly HomePageViewModel _vm;

    public HomePage(HomePageViewModel vm)
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
