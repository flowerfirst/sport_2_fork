using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using oculus_sport.Models;
using oculus_sport.Services.Other;
using oculus_sport.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace oculus_sport.ViewModels.Main;

public partial class NotificationPageViewModel : BaseViewModel
{
    private readonly NotificationStore _notificationStore;

    private readonly ObservableCollection<NotificationItem> _notifications = new();

    private bool _isRefreshing;

    public ObservableCollection<NotificationItem> Notifications => _notifications;

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    public NotificationPageViewModel(NotificationStore notificationStore)
    {
        _notificationStore = notificationStore;
        Title = "Notifications";
    }

    public async Task InitializeAsync()
    {
        await LoadNotificationsAsync();
        await _notificationStore.MarkAllAsReadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        await LoadNotificationsAsync();
        IsRefreshing = false;
    }

    private async Task LoadNotificationsAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            var items = await _notificationStore.GetNotificationsAsync();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Notifications.Clear();
                foreach (var notification in items)
                {
                    Notifications.Add(notification);
                }
            });
        }
        finally
        {
            IsBusy = false;
        }
    }
}