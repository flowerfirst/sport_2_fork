using System;
using System.Text.Json.Serialization;

namespace oculus_sport.Models;

public class NotificationItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsRead { get; set; }

    [JsonIgnore]
    public string RelativeTime => FormatRelativeTime();

    private string FormatRelativeTime()
    {
        var span = DateTimeOffset.UtcNow - CreatedAt;
        if (span.TotalSeconds < 60)
            return "Just now";
        if (span.TotalMinutes < 60)
            return $"{(int)span.TotalMinutes} min ago";
        if (span.TotalHours < 24)
            return $"{(int)span.TotalHours} hr ago";
        if (span.TotalDays < 7)
            return $"{(int)span.TotalDays} d ago";
        return CreatedAt.LocalDateTime.ToString("MMM d, h:mm tt");
    }
}