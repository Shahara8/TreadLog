using System.Collections.ObjectModel;
using System.Windows.Input;
using TreadLog.Helpers;
using TreadLog.Models;
using TreadLog.Services.Interfaces;
using TreadLog.ViewModels.Base;

namespace TreadLog.ViewModels;

/// <summary>
/// History screen ViewModel.
/// Loads all sessions from the repository, supports search/filter by date,
/// and exposes edit/delete commands that delegate back to the LogWorkoutViewModel.
/// </summary>
public class HistoryViewModel : ViewModelBase
{
    private readonly IWorkoutRepository       _workoutRepo;
    private readonly IUserSettingsRepository  _settingsRepo;
    private readonly IDataPortabilityService? _portabilityService;

    // ── Session list ──────────────────────────────────────────────────────────

    public ObservableCollection<WorkoutSessionRow> Sessions { get; } = new();

    // ── Search / filter ───────────────────────────────────────────────────────

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value)) return;
            ApplyFilter();
        }
    }

    private DateTime? _filterStartDate;
    public DateTime? FilterStartDate
    {
        get => _filterStartDate;
        set
        {
            SetProperty(ref _filterStartDate, value);
            (_applyDateFilterCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private DateTime? _filterEndDate;
    public DateTime? FilterEndDate
    {
        get => _filterEndDate;
        set
        {
            SetProperty(ref _filterEndDate, value);
            (_applyDateFilterCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    private WorkoutSessionRow? _selectedSession;
    public WorkoutSessionRow? SelectedSession
    {
        get => _selectedSession;
        set
        {
            SetProperty(ref _selectedSession, value);
            (_editCommand   as RelayCommand)?.RaiseCanExecuteChanged();
            (_deleteCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    // ── Loading / busy ────────────────────────────────────────────────────────

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            SetProperty(ref _isBusy, value);
            (_deleteCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    // ── Navigation / edit callback ────────────────────────────────────────────

    /// <summary>
    /// Raised when the user requests to edit a session.
    /// The subscriber (e.g., MainViewModel) should navigate to LogWorkoutViewModel
    /// and call LoadSession(session) on it.
    /// </summary>
    public event EventHandler<WorkoutSession>? EditRequested;

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand LoadDataCommand { get; }

    private readonly ICommand _applyDateFilterCommand;
    public ICommand ApplyDateFilterCommand => _applyDateFilterCommand;

    public ICommand ClearFilterCommand { get; }

    private readonly ICommand _editCommand;
    public ICommand EditCommand => _editCommand;

    private readonly ICommand _deleteCommand;
    public ICommand DeleteCommand => _deleteCommand;

    // Export/import take a file path as the command parameter (set by the View after dialog)
    public ICommand ExportCsvCommand  { get; }
    public ICommand ExportJsonCommand { get; }
    public ICommand ImportCommand     { get; }

    // ── Import status ─────────────────────────────────────────────────────────

    private string _importStatusMessage = string.Empty;
    public string ImportStatusMessage
    {
        get => _importStatusMessage;
        private set => SetProperty(ref _importStatusMessage, value);
    }

    // ── Backing full list (pre-filter) ────────────────────────────────────────

    private List<WorkoutSessionRow> _allRows = new();
    private string _currentUnit = "km";

    // ── Constructor ───────────────────────────────────────────────────────────

    public HistoryViewModel(
        IWorkoutRepository        workoutRepo,
        IUserSettingsRepository   settingsRepo,
        IDataPortabilityService?  portabilityService = null)
    {
        _workoutRepo        = workoutRepo        ?? throw new ArgumentNullException(nameof(workoutRepo));
        _settingsRepo       = settingsRepo       ?? throw new ArgumentNullException(nameof(settingsRepo));
        _portabilityService = portabilityService;

        LoadDataCommand = new RelayCommand(async () => await LoadDataAsync());

        _applyDateFilterCommand = new RelayCommand(
            async () => await LoadDataAsync(),
            () => FilterStartDate.HasValue || FilterEndDate.HasValue);

        ClearFilterCommand = new RelayCommand(() =>
        {
            FilterStartDate = null;
            FilterEndDate   = null;
            SearchText      = string.Empty;
            ApplyFilter();
        });

        _editCommand = new RelayCommand(
            () =>
            {
                if (SelectedSession != null)
                    EditRequested?.Invoke(this, SelectedSession.Source);
            },
            () => SelectedSession != null);

        _deleteCommand = new RelayCommand(
            async () => await DeleteSelectedAsync(),
            () => SelectedSession != null && !IsBusy);

        ExportCsvCommand  = new RelayCommand(async p => await ExportCsvAsync(p as string ?? ""));
        ExportJsonCommand = new RelayCommand(async p => await ExportJsonAsync(p as string ?? ""));
        ImportCommand     = new RelayCommand(async p => await ImportAsync(p as string ?? ""));
    }

    // ── Data loading ──────────────────────────────────────────────────────────

    public async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            var settings = await _settingsRepo.GetAsync();
            _currentUnit = settings.DistanceUnit;

            IEnumerable<WorkoutSession> sessions;
            if (FilterStartDate.HasValue || FilterEndDate.HasValue)
            {
                var now   = DateTime.UtcNow;
                var start = (FilterStartDate ?? DateTime.MinValue).ToUniversalTime().ToString("o");
                var end   = (FilterEndDate   ?? now).ToUniversalTime().ToString("o");
                sessions  = await _workoutRepo.GetByDateRangeAsync(start, end);
            }
            else
            {
                sessions = await _workoutRepo.GetAllAsync();
            }

            _allRows = sessions
                .Select(s => new WorkoutSessionRow(s, _currentUnit))
                .ToList();

            ApplyFilter();
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    public async Task DeleteSelectedAsync()
    {
        if (SelectedSession == null) return;

        IsBusy = true;
        try
        {
            int id = SelectedSession.Source.Id;
            await _workoutRepo.DeleteAsync(id);

            _allRows.Remove(SelectedSession);
            Sessions.Remove(SelectedSession);
            SelectedSession = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Export / Import ───────────────────────────────────────────────────────

    public async Task ExportCsvAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || _portabilityService == null) return;
        IsBusy = true;
        try { await _portabilityService.ExportToCsvAsync(_allRows.Select(r => r.Source), filePath); }
        finally { IsBusy = false; }
    }

    public async Task ExportJsonAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || _portabilityService == null) return;
        IsBusy = true;
        try { await _portabilityService.ExportToJsonAsync(_allRows.Select(r => r.Source), filePath); }
        finally { IsBusy = false; }
    }

    public async Task ImportAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || _portabilityService == null) return;
        IsBusy = true;
        try
        {
            var result = await _portabilityService.ImportFromFileAsync(filePath);
            ImportStatusMessage = result.Errors.Count > 0
                ? $"Imported {result.InsertedRows} / {result.TotalRows} rows. {result.Errors.Count} error(s)."
                : $"Imported {result.InsertedRows} of {result.TotalRows} rows ({result.SkippedRows} duplicates skipped).";
            await LoadDataAsync();
        }
        finally { IsBusy = false; }
    }

    // ── Filter ────────────────────────────────────────────────────────────────

    private void ApplyFilter()
    {
        var filtered = _allRows.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            filtered = filtered.Where(r =>
                r.DateDisplay.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.DistanceDisplay.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.DurationDisplay.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        Sessions.Clear();
        foreach (var row in filtered)
            Sessions.Add(row);
    }
}

/// <summary>
/// Display-ready projection of a WorkoutSession for use in the History list.
/// Holds pre-formatted strings so the View binds directly without converters.
/// </summary>
public sealed class WorkoutSessionRow
{
    public WorkoutSession Source { get; }

    public string DateDisplay     { get; }
    public string DistanceDisplay { get; }
    public string DurationDisplay { get; }
    public string PaceDisplay     { get; }
    public string SpeedDisplay    { get; }
    public string InclineDisplay  { get; }
    public string CaloriesDisplay { get; }
    public string HeartRateDisplay{ get; }

    public WorkoutSessionRow(WorkoutSession source, string unit)
    {
        Source = source;

        DateDisplay = source.SessionDate.ToLocalTime().ToString("MMM d, yyyy");

        double displayDist = unit == "km"
            ? source.DistanceKm
            : UnitConverter.KmToMiles(source.DistanceKm);
        DistanceDisplay = $"{displayDist:F2} {unit}";

        int h   = source.DurationSeconds / 3600;
        int m   = (source.DurationSeconds % 3600) / 60;
        int sec = source.DurationSeconds % 60;
        DurationDisplay = h > 0
            ? $"{h}:{m:D2}:{sec:D2}"
            : $"{m}:{sec:D2}";

        PaceDisplay  = UnitConverter.CalculatePaceString(source.DistanceKm, source.DurationSeconds, unit);

        double displaySpeed = unit == "km"
            ? source.AvgSpeedKmh
            : UnitConverter.KmhToMph(source.AvgSpeedKmh);
        SpeedDisplay = $"{displaySpeed:F1} {(unit == "km" ? "km/h" : "mph")}";

        InclineDisplay   = $"{source.InclinePercent:F1}%";
        CaloriesDisplay  = source.CaloriesBurned.HasValue ? $"{source.CaloriesBurned} kcal" : "--";
        HeartRateDisplay = source.AvgHeartRate.HasValue ? $"{source.AvgHeartRate} bpm" : "--";
    }
}
