using System.Windows.Input;
using TreadLog.Models;
using TreadLog.ViewModels.Base;
using TreadLog.Services.Interfaces;

namespace TreadLog.ViewModels;

/// <summary>
/// Settings screen ViewModel.
/// Loads the single UserSettings row on activation, lets the user toggle the
/// distance unit, and persists changes via SaveCommand.
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    private readonly IUserSettingsRepository _settingsRepo;

    // ── Unit preference ───────────────────────────────────────────────────────

    private string _distanceUnit = "km";
    public string DistanceUnit
    {
        get => _distanceUnit;
        set
        {
            if (!SetProperty(ref _distanceUnit, value)) return;
            (_saveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    // Available options for the View's radio buttons / combo-box
    public static IReadOnlyList<string> AvailableUnits { get; } = new[] { "km", "mi" };

    // ── State flags ───────────────────────────────────────────────────────────

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            SetProperty(ref _isBusy, value);
            (_saveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private bool _saveSucceeded;
    public bool SaveSucceeded
    {
        get => _saveSucceeded;
        private set => SetProperty(ref _saveSucceeded, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand LoadCommand { get; }

    private readonly ICommand _saveCommand;
    public ICommand SaveCommand => _saveCommand;

    // ── Cached original unit — used to detect dirty state ────────────────────

    private string _loadedUnit = "km";

    // ── Constructor ───────────────────────────────────────────────────────────

    public SettingsViewModel(IUserSettingsRepository settingsRepo)
    {
        _settingsRepo = settingsRepo ?? throw new ArgumentNullException(nameof(settingsRepo));

        LoadCommand = new RelayCommand(async () => await LoadAsync());

        _saveCommand = new RelayCommand(
            async () => await SaveAsync(),
            () => !IsBusy && DistanceUnit != _loadedUnit);
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var settings  = await _settingsRepo.GetAsync();
            _loadedUnit   = settings.DistanceUnit;
            DistanceUnit  = settings.DistanceUnit;
            SaveSucceeded = false;
            StatusMessage = string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    public async Task SaveAsync()
    {
        IsBusy = true;
        SaveSucceeded = false;
        StatusMessage = string.Empty;
        try
        {
            var settings = new UserSettings
            {
                Id           = 1,
                DistanceUnit = DistanceUnit,
                UpdatedAt    = DateTime.UtcNow.ToString("o")
            };
            await _settingsRepo.SaveAsync(settings);
            _loadedUnit   = DistanceUnit;
            SaveSucceeded = true;
            StatusMessage = "Settings saved.";
            (_saveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        catch
        {
            StatusMessage = "Failed to save settings. Please try again.";
            SaveSucceeded = false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
