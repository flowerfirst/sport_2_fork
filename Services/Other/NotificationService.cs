using System;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using oculus_sport.Models;

#if ANDROID
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using AndroidX.Core.App;
using AndroidX.Core.Content;
#endif

namespace oculus_sport.Services.Other
{
    public class NotificationService
    {
        private readonly NotificationStore _notificationStore;

        public NotificationService(NotificationStore notificationStore)
        {
            _notificationStore = notificationStore;
        }
#if ANDROID
        private const string ChannelId = "oculus_sport_general";
        private bool _channelRegistered;
#endif

        public async Task NotifyBookingConfirmedAsync(Booking booking)
        {
            var message = $"{booking.FacilityName} • {booking.Date:MMM d} • {booking.TimeSlot}";
            await ShowNotificationAsync("Booking confirmed", message);
        }

        public async Task ShowNotificationAsync(string title, string message)
        {
            var notification = new NotificationItem
            {
                Title = title,
                Message = message,
                CreatedAt = DateTimeOffset.UtcNow,
                IsRead = false
            };

            await _notificationStore.AddNotificationAsync(notification);
#if ANDROID
            await ShowAndroidNotificationAsync(title, message);
#else
            await MainThread.InvokeOnMainThreadAsync(() =>
                Microsoft.Maui.Controls.Application.Current?.MainPage?.DisplayAlert(title, message, "OK"));
#endif
        }

#if ANDROID
        private async Task ShowAndroidNotificationAsync(string title, string message)
        {
            if (!await EnsureAndroidNotificationPermissionAsync())
                return;

            var context = Platform.CurrentActivity ?? global::Android.App.Application.Context;
            if (context == null)
                return;

            await EnsureChannelAsync(context);

            var builder = new NotificationCompat.Builder(context, ChannelId)
                .SetContentTitle(title)
                .SetContentText(message)
                .SetStyle(new NotificationCompat.BigTextStyle().BigText(message))
                .SetSmallIcon(context.ApplicationInfo?.Icon ?? Android.Resource.Drawable.IcDialogInfo)
                .SetAutoCancel(true)
                .SetPriority(NotificationCompat.PriorityHigh);

            var notificationId = (int)(DateTime.UtcNow.Ticks % int.MaxValue);
            NotificationManagerCompat.From(context).Notify(notificationId, builder.Build());
        }

        private Task EnsureChannelAsync(Context context)
        {
            if (_channelRegistered || !OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                _channelRegistered = true;
                return Task.CompletedTask;
            }

            var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
            var channel = new NotificationChannel(ChannelId, "Oculus Sports", NotificationImportance.High)
            {
                Description = "Booking confirmations and alerts"
            };
            manager?.CreateNotificationChannel(channel);
            _channelRegistered = true;
            return Task.CompletedTask;
        }

        private Task<bool> EnsureAndroidNotificationPermissionAsync()
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(33))
                return Task.FromResult(true);

            var context = Platform.CurrentActivity ?? global::Android.App.Application.Context;
            if (context == null)
                return Task.FromResult(false);

            if (ContextCompat.CheckSelfPermission(context, Manifest.Permission.PostNotifications) == Permission.Granted)
                return Task.FromResult(true);

            var activity = Platform.CurrentActivity;
            if (activity != null)
            {
                ActivityCompat.RequestPermissions(activity, new[] { Manifest.Permission.PostNotifications }, 1001);
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Microsoft.Maui.Controls.Application.Current?.MainPage?.DisplayAlert(
                        "Enable notifications",
                        "Notifications are currently disabled. Please allow the permission in the next prompt to receive booking alerts.",
                        "OK");
                });
            }

            return Task.FromResult(false);
        }
#endif
    }
}
