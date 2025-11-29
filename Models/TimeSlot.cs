using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace oculus_sport.Models;

// TimeSlot is used to display availability on the Schedule Page.
public partial class TimeSlot : ObservableObject
{
    // Merged: Kept your 'TimeRange' for backward compatibility with your BookingViewModel,
    // but also added 'SlotName' (likely from your partner) just in case they use it elsewhere.
    // Ideally, you should refactor to use just one, but this prevents build errors for now.
    public string TimeRange { get; set; } = string.Empty;

    [ObservableProperty]
    private string _slotName = string.Empty; // e.g., "10:00 - 11:00"

    public TimeSpan StartTime { get; set; }

    // Made IsAvailable an ObservableProperty for consistent MVVM binding (Partner's improvement)
    [ObservableProperty]
    private bool _isAvailable = true;

    [ObservableProperty]
    private bool _isSelected;
}