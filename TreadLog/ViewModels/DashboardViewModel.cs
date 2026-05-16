using System.Collections.ObjectModel;
using System.Windows.Input;
using TreadLog.Helpers;
using TreadLog.Services.Interfaces;
using TreadLog.ViewModels.Base;
using TreadLog.ViewModels.Support;

namespace TreadLog.ViewModels;

/// <summary>
/// Landing screen ViewModel.
/// Aggregates KPI cards and chart series from the repository,
/// respecting the active date-filter selection and user unit preference.
/// </summary>
public class DashboardViewModel : ViewModelBase
{
    private readonly IWorkoutRepository      _workoutRepo;
    private readonly IUserSettingsRepository _settingsRepo;

    // ── KPI display strings ────────────────────────────────────────────────────

    private string _totalDistanceDisplay = "--";
    public string TotalDistanceDisplay
    {
        get => _totalDistanceDisplay;
        private set => SetProperty(ref _totalDistanceDisplay, value);
    }

    private string _totalTimeDisplay = "--";
    public string TotalTimeDisplay
    {
        get => _totalTimeDisplay;
        private set => SetProperty(ref _totalTimeDisplay, value);
    }

    private string _averagePaceDisplay = "--:--";
    public string AveragePaceDisplay
    {
        get => _averagePaceDisplay;
        private set => SetProperty(ref _averagePaceDisplay, value);
    }

    private int _sessionCount;
    public int SessionCount
    {
        get => _sessionCount;
        private set => SetProperty(ref _sessionCount, value);
    }

    // ── Loading state ─────────────────────────────────────────────────────────

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    // ── Date filter ───────────────────────────────────────────────────────────

    private DateFilterOption _selectedFilter = DateFilterOption.Last30Days;
    public DateFilterOption SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (!SetProperty(ref _selectedFilter, value)) return;
            // Auto-reload for preset filters; Custom requires explicit Apply
            if (value != DateFilterOption.Custom)
                _ = LoadDataAsync();
            (_applyCustomFilterCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private DateTime? _customStartDate;
    public DateTime? CustomStartDate
    {
        get => _customStartDate;
        set
        {
            SetProperty(ref _customStartDate, value);
            (_applyCustomFilterCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private DateTime? _customEndDate;
    public DateTime? CustomEndDate
    {
        get => _customEndDate;
        set
        {
            SetProperty(ref _customEndDate, value);
            (_applyCustomFilterCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    // ── Chart data ────────────────────────────────────────────────────────────

    /// <summary>Weekly distance totals for the bar chart.</summary>
    public ObservableCollection<ChartDataPoint> WeeklyDistancePoints { get; } = new();

    /// <summary>Per-session average speed for the trend line chart.</summary>
    public ObservableCollection<ChartDataPoint> SpeedTrendPoints { get; } = new();

    /// <summary>Per-session incline percentage for the trend line chart.</summary>
    public ObservableCollection<ChartDataPoint> InclineTrendPoints { get; } = new();

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand LoadDataCommand { get; }

    private readonly ICommand _applyCustomFilterCommand;
    public ICommand ApplyCustomFilterCommand => _applyCustomFilterCommand;

    // ── Constructor ───────────────────────────────────────────────────────────

    public DashboardViewModel(
        IWorkoutRepository      workoutRepo,
        IUserSettingsRepository settingsRepo)
    {
        _workoutRepo  = workoutRepo  ?? throw new ArgumentNullException(nameof(workoutRepo));
        _settingsRepo = settingsRepo ?? throw new ArgumentNullException(nameof(settingsRepo));

        LoadDataCommand = new RelayCommand(async () => await LoadDataAsync());

        _applyCustomFilterCommand = new RelayCommand(
            async () => await LoadDataAsync(),
            () => SelectedFilter == DateFilterOption.Custom
                  && CustomStartDate.HasValue
                  && CustomEndDate.HasValue
                  && CustomStartDate <= CustomEndDate);
    }

    // ── Data loading ──────────────────────────────────────────────────────────

    public async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            var settings = await _settingsRepo.GetAsync();
            var unit     = settings.DistanceUnit;

            var (startIso, endIso) = GetDateRange();
            var sessions = (await _workoutRepo.GetByDateRangeAsync(startIso, endIso)).ToList();

            SessionCount = sessions.Count;

            if (sessions.Count == 0)
            {
                TotalDistanceDisplay = unit == "km" ? "0.0 km" : "0.0 mi";
                TotalTimeDisplay     = "0 h, 0 min";
                AveragePaceDisplay   = "--:--";
            }
            else
            {
                double totalKm  = sessions.Sum(s => s.DistanceKm);
                int    totalSec = sessions.Sum(s => s.DurationSeconds);
                int    hours    = totalSec / 3600;
                int    minutes  = (totalSec % 3600) / 60;

                TotalDistanceDisplay = unit == "km"
                    ? $"{totalKm:F1} km"
                    : $"{UnitConverter.KmToMiles(totalKm):F1} mi";

                TotalTimeDisplay   = $"{hours} h, {minutes} min";
                AveragePaceDisplay = UnitConverter.CalculatePaceString(totalKm, totalSec, unit);
            }

            BuildChartData(sessions, unit);
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private (string startIso, string endIso) GetDateRange()
    {
        var now = DateTime.UtcNow;
        return SelectedFilter switch
        {
            DateFilterOption.Last7Days  => (now.AddDays(-7).ToString("o"),    now.ToString("o")),
            DateFilterOption.Last30Days => (now.AddDays(-30).ToString("o"),   now.ToString("o")),
            DateFilterOption.YearToDate => (new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToString("o"), now.ToString("o")),
            DateFilterOption.Custom     => (
                (CustomStartDate ?? now.AddDays(-30)).ToUniversalTime().ToString("o"),
                (CustomEndDate   ?? now).ToUniversalTime().ToString("o")),
            _ => (now.AddDays(-30).ToString("o"), now.ToString("o"))
        };
    }

    private void BuildChartData(List<Models.WorkoutSession> sessions, string unit)
    {
        // ── Weekly distance bar chart ──────────────────────────────────────────
        WeeklyDistancePoints.Clear();

        var weeklyGroups = sessions
            .GroupBy(s => StartOfWeek(s.SessionDate))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                double distKm = g.Sum(s => s.DistanceKm);
                double display = unit == "km" ? distKm : UnitConverter.KmToMiles(distKm);
                return new ChartDataPoint(g.Key.ToString("MMM d"), Math.Round(display, 2));
            });

        foreach (var pt in weeklyGroups)
            WeeklyDistancePoints.Add(pt);

        // ── Speed / Incline trend lines ────────────────────────────────────────
        SpeedTrendPoints.Clear();
        InclineTrendPoints.Clear();

        var ordered = sessions.OrderBy(s => s.SessionDate).ToList();
        foreach (var s in ordered)
        {
            string label    = s.SessionDate.ToString("MM/dd");
            double speed    = unit == "km" ? s.AvgSpeedKmh : UnitConverter.KmhToMph(s.AvgSpeedKmh);
            SpeedTrendPoints.Add(new ChartDataPoint(label, Math.Round(speed, 1)));
            InclineTrendPoints.Add(new ChartDataPoint(label, s.InclinePercent));
        }
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }
}
