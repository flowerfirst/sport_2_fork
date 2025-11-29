using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using oculus_sport.Models;
using oculus_sport.ViewModels.Base;

namespace oculus_sport.ViewModels.Main
{
    public partial class HomePageViewModel : BaseViewModel
    {
        [ObservableProperty]
        private string _userName = "Tony";

        [ObservableProperty]
        private ObservableCollection<SportCategory> _categories = new();

        [ObservableProperty]
        private ObservableCollection<Facility> _facilities = new();

        private List<Facility> _allFacilities = new();

        public HomePageViewModel()
        {
            Title = "Home";
            LoadData();
        }

        private void LoadData()
        {
            Categories.Add(new SportCategory { Name = "Badminton", IsSelected = true });
            Categories.Add(new SportCategory { Name = "Ping-Pong" });
            Categories.Add(new SportCategory { Name = "Basketball" });

            _allFacilities.Clear();
            for (int i = 1; i <= 3; i++)
                _allFacilities.Add(new Facility { Name = $"Badminton Court {i}", Location = "UTS Indoor Hall", Price = "Free", Rating = 4.5, ImageUrl = "court_badminton.png" });

            for (int i = 1; i <= 4; i++)
                _allFacilities.Add(new Facility { Name = $"Ping-Pong Table {i}", Location = "Student Center L2", Price = "Free", Rating = 4.8, ImageUrl = "court_pingpong.png" });

            _allFacilities.Add(new Facility { Name = "Basketball Court 1", Location = "Outdoor Complex", Price = "Free", Rating = 4.2, ImageUrl = "court_basketball.png" });

            FilterFacilities("Badminton");
        }

        [RelayCommand]
        void SelectCategory(SportCategory category)
        {
            if (Categories == null) return;
            foreach (var c in Categories) c.IsSelected = false;
            category.IsSelected = true;
            FilterFacilities(category.Name);
        }

        private void FilterFacilities(string categoryName)
        {
            Facilities.Clear();
            var filtered = _allFacilities.Where(f => f.Name.Contains(categoryName, StringComparison.OrdinalIgnoreCase));
            foreach (var facility in filtered) Facilities.Add(facility);
        }

        [RelayCommand]
        async Task BookFacility(Facility facility)
        {
            var navigationParameter = new Dictionary<string, object> { { "Facility", facility } };
            await Shell.Current.GoToAsync("BookingPage", navigationParameter);
        }

        [RelayCommand]
        async Task GoToNotifications()
        {
            await Shell.Current.GoToAsync("NotificationPage");
        }
    }
}