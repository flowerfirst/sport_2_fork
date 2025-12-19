using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using oculus_sport.Models;

namespace oculus_sport.Services.Other;

public class NotificationStore
{
    private const string StorageKey = "LocalNotificationsCache";
    private const int MaxItems = 50;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task<IReadOnlyList<NotificationItem>> GetNotificationsAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var items = ReadFromPreferences();
            return items
                .OrderByDescending(n => n.CreatedAt)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddNotificationAsync(NotificationItem notification)
    {
        await _gate.WaitAsync();
        try
        {
            var items = ReadFromPreferences();
            items.Insert(0, notification);
            if (items.Count > MaxItems)
            {
                items = items.Take(MaxItems).ToList();
            }
            WriteToPreferences(items);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MarkAllAsReadAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var items = ReadFromPreferences();
            if (items.Count == 0)
                return;

            foreach (var notification in items)
            {
                notification.IsRead = true;
            }

            WriteToPreferences(items);
        }
        finally
        {
            _gate.Release();
        }
    }

    private List<NotificationItem> ReadFromPreferences()
    {
        var json = Preferences.Default.Get(StorageKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
            return new List<NotificationItem>();

        try
        {
            var items = JsonSerializer.Deserialize<List<NotificationItem>>(json, _jsonOptions);
            return items ?? new List<NotificationItem>();
        }
        catch
        {
            Preferences.Default.Remove(StorageKey);
            return new List<NotificationItem>();
        }
    }

    private void WriteToPreferences(List<NotificationItem> items)
    {
        var json = JsonSerializer.Serialize(items, _jsonOptions);
        Preferences.Default.Set(StorageKey, json);
    }
}
