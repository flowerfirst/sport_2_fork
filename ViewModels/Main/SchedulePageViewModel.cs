using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using oculus_sport.Models;
using oculus_sport.ViewModels.Base;

namespace oculus_sport.ViewModels.Main;

public partial class SchedulePageViewModel : BaseViewModel
{
    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Now;

    // NEW: Restricts the DatePicker to prevent past dates
    [ObservableProperty]
    private DateTime _minimumDate = DateTime.Today;

    [ObservableProperty]
    private ObservableCollection<SportCategory> _categories = new();

    [ObservableProperty]
    private ObservableCollection<Facility> _availableFacilities = new();

    public SchedulePageViewModel()
    {
        Title = "Check Availability";
        LoadCategories();
        MainThread.BeginInvokeOnMainThread(async () => await LoadAvailability());
    }

    private void LoadCategories()
    {
        Categories = new ObservableCollection<SportCategory>
        {
            new SportCategory { Name = "Badminton", IsSelected = true },
            new SportCategory { Name = "Ping-Pong" },
            new SportCategory { Name = "Basketball" }
        };
    }

    async partial void OnSelectedDateChanged(DateTime value)
    {
        await LoadAvailability();
    }

    [RelayCommand]
    async Task SelectCategory(SportCategory category)
    {
        if (Categories == null) return;
        foreach (var c in Categories) c.IsSelected = false;
        category.IsSelected = true;

        await LoadAvailability();
    }

    private async Task LoadAvailability()
    {
        IsBusy = true;
        await Task.Delay(300);

        AvailableFacilities.Clear();

        var selectedCategory = Categories.FirstOrDefault(c => c.IsSelected)?.Name;

        if (selectedCategory == "Badminton")
        {
            var day = SelectedDate.DayOfWeek;
            if (day == DayOfWeek.Monday || day == DayOfWeek.Thursday || day == DayOfWeek.Friday)
            {
                for (int i = 1; i <= 3; i++)
                    AvailableFacilities.Add(new Facility { FacilityName = $"Badminton Court {i}", Location = "10:00, 12:00, 14:00", ImageUrl = "badminton_court.webp" });
            }
        }
        else if (selectedCategory == "Ping-Pong")
        {
            var day = SelectedDate.DayOfWeek;
            if (day == DayOfWeek.Monday || day == DayOfWeek.Friday)
            {
                for (int i = 1; i <= 4; i++)
                    AvailableFacilities.Add(new Facility { FacilityName = $"Ping-Pong Table {i}", Location = "10:00, 12:00, 14:00", ImageUrl = "pingpong_court.jpg" });
            }
        }
        else if (selectedCategory == "Basketball")
        {
            var day = SelectedDate.DayOfWeek;
            if (day != DayOfWeek.Saturday && day != DayOfWeek.Sunday)
            {
                AvailableFacilities.Add(new Facility { FacilityName = "Basketball Court 1", Location = "10:00, 12:00, 14:00, 16:00", ImageUrl = "basketball_court.webp" });
            }
        }

        if (AvailableFacilities.Count == 0)
        {
            AvailableFacilities.Add(new Facility { FacilityName = "No Courts Available", Location = "Closed on this day", ImageUrl = "dotnet_bot.png" });
        }

        IsBusy = false;
    }
}