using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using oculus_sport.Models;
using oculus_sport.Services.Auth;
using oculus_sport.Services.Storage;
using oculus_sport.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace oculus_sport.ViewModels.Main;

public partial class PriceListViewModel : BaseViewModel
{
    private readonly FirebaseDataService _dataService;
    private readonly IAuthService _authService;

    private ObservableCollection<Facility> _prices = new();
    public ObservableCollection<Facility> Prices
    {
        get => _prices;
        set => SetProperty(ref _prices, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public PriceListViewModel(FirebaseDataService dataService, IAuthService authService)
    {
        _dataService = dataService;
        _authService = authService;
        Title = "Price List";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            var idToken = await SecureStorage.GetAsync("idToken");
            if (string.IsNullOrEmpty(idToken) || IsTokenExpired(idToken))
            {
                idToken = await _authService.RefreshIdTokenAsync();
            }

            if (string.IsNullOrEmpty(idToken))
            {
                StatusMessage = "Connect to the internet to view the latest prices.";
                Prices.Clear();
                return;
            }

            var facilities = await _dataService.GetFacilitiesAsync(idToken);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Prices.Clear();
                foreach (var facility in facilities.OrderBy(f => f.Category).ThenBy(f => f.FacilityName))
                {
                    Prices.Add(facility);
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PriceList] Failed to load prices: {ex.Message}");
            StatusMessage = "Unable to load prices right now.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static bool IsTokenExpired(string idToken)
    {
        try
        {
            var parts = idToken.Split('.');
            if (parts.Length != 3) return true;

            var payload = parts[1];
            var jsonBytes = Convert.FromBase64String(payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '='));
            var json = System.Text.Encoding.UTF8.GetString(jsonBytes);

            var match = Regex.Match(json, "\"exp\":(\\d+)");
            if (!match.Success) return true;

            var expUnix = long.Parse(match.Groups[1].Value);
            var expDate = DateTimeOffset.FromUnixTimeSeconds(expUnix);
            return expDate < DateTimeOffset.UtcNow;
        }
        catch
        {
            return true;
        }
    }
}
