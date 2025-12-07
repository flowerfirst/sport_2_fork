using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using oculus_sport.Models;
using oculus_sport.ViewModels.Base;
using System.Collections.ObjectModel;

namespace oculus_sport.ViewModels.Main;

public partial class EventPageViewModel : BaseViewModel
{
    [ObservableProperty]
    private ObservableCollection<SportEvent> _events = new();

    public EventPageViewModel()
    {
        Title = "Events & News";
        LoadEvents();
    }

    private void LoadEvents()
    {
        Events = new ObservableCollection<SportEvent>
        {
            new SportEvent
            {
                Title = "Badminton Open 2025",
                Description = "Join the biggest campus tournament of the year! Registration ends soon.",
                DateDisplay = "Nov 12",
                IsNew = true,
                ImageUrl = "badminton_court.png"
            },
            new SportEvent
            {
                Title = "Court Maintenance",
                Description = "Tennis Court A will be closed for resurfacing this weekend.",
                DateDisplay = "2 days ago",
                IsNew = false,
                ImageUrl = "tennis.jpg"
            },
            new SportEvent
            {
                Title = "Pickleball Workshop",
                Description = "Free coaching session for beginners. Equipment provided.",
                DateDisplay = "Oct 30",
                IsNew = false,
                ImageUrl = "pickleball.jpg"
            }
        };
    }

    [RelayCommand]
    async Task GoToDetails(SportEvent sportEvent)
    {
        if (sportEvent == null) return;

        var navigationParameter = new Dictionary<string, object>
        {
            { "SportEvent", sportEvent }
        };

        await Shell.Current.GoToAsync(nameof(Views.Main.EventDetailsPage), navigationParameter);
    }
}