using System.Globalization;
using System.Windows.Input;
using TreadLog.Helpers;
using TreadLog.Models;
using TreadLog.Services.Interfaces;
using TreadLog.ViewModels.Base;

namespace TreadLog.ViewModels;

/// <summary>
/// ViewModel for creating and editing a single workout session.
/// Stores metric values internally (km, km/h); displays values in the user's preferred unit.
/// All validation is performed by <see cref="WorkoutValidator"/> and <see cref="UnitConverter"/>.
/// The consistency check raises a warning but does NOT block saving.
/// </summary>
public class LogWorkoutViewModel : ViewModelBase
{
    private readonly IWorkoutRepository      _repo;
    private readonly IUserSettingsRepository _settingsRepo;

    /// <summary>Raised after a successful save so the shell can navigate away.</summary>
    public event EventHandler? SaveSucceeded;

    // ── Edit mode tracking ────────────────────────────────────────────────────

    private int? _editingSessionId;
    public bool IsEditing => _editingSessionId.HasValue;

    // ── Internal metric state (never exposed directly to the View) ─────────────

    private double _distanceKm;
    private double _avgSpeedKmh;
    private int    _durationSeconds;
    private double _inclinePercent;
    private int?   _calories;
    private int?   _heartRate;

    // ── Input properties (string for TextBox binding) ──────────────────────────

    private string _distanceInput = "";
    public string DistanceInput
    {
        get => _distanceInput;
        set { SetProperty(ref _distanceInput, value); ParseAndValidateDistance(); }
    }

    private string _speedInput = "";
    public string SpeedInput
    {
        get => _speedInput;
        set { SetProperty(ref _speedInput, value); ParseAndValidateSpeed(); }
    }

    private string _durationHoursInput = "0";
    public string DurationHoursInput
    {
        get => _durationHoursInput;
        set { SetProperty(ref _durationHoursInput, value); ParseAndValidateDuration(); }
    }

    private string _durationMinutesInput = "0";
    public string DurationMinutesInput
    {
        get => _durationMinutesInput;
        set { SetProperty(ref _durationMinutesInput, value); ParseAndValidateDuration(); }
    }

    private string _durationSecondsInput = "0";
    public string DurationSecondsInput
    {
        get => _durationSecondsInput;
        set { SetProperty(ref _durationSecondsInput, value); ParseAndValidateDuration(); }
    }

    private string _inclineInput = "0";
    public string InclineInput
    {
        get => _inclineInput;
        set { SetProperty(ref _inclineInput, value); ParseAndValidateIncline(); }
    }

    private string _caloriesInput = "";
    public string CaloriesInput
    {
        get => _caloriesInput;
        set { SetProperty(ref _caloriesInput, value); ParseAndValidateCalories(); }
    }

    private string _heartRateInput = "";
    public string HeartRateInput
    {
        get => _heartRateInput;
        set { SetProperty(ref _heartRateInput, value); ParseAndValidateHeartRate(); }
    }

    private DateTime _sessionDate = DateTime.Now;
    public DateTime SessionDate
    {
        get => _sessionDate;
        set => SetProperty(ref _sessionDate, value);
    }

    private string _selectedUnit = "km";
    public string SelectedUnit
    {
        get => _selectedUnit;
        set
        {
            if (_selectedUnit == value) return;
            string oldUnit = _selectedUnit;
            SetProperty(ref _selectedUnit, value);
            ConvertDisplayValuesOnUnitChange(oldUnit, value);
        }
    }

    // ── Error / warning properties ────────────────────────────────────────────

    private string _distanceError = "";
    public string DistanceError { get => _distanceError; private set => SetProperty(ref _distanceError, value); }

    private string _speedError = "";
    public string SpeedError { get => _speedError; private set => SetProperty(ref _speedError, value); }

    private string _durationError = "";
    public string DurationError { get => _durationError; private set => SetProperty(ref _durationError, value); }

    private string _inclineError = "";
    public string InclineError { get => _inclineError; private set => SetProperty(ref _inclineError, value); }

    private string _caloriesError = "";
    public string CaloriesError { get => _caloriesError; private set => SetProperty(ref _caloriesError, value); }

    private string _heartRateError = "";
    public string HeartRateError { get => _heartRateError; private set => SetProperty(ref _heartRateError, value); }

    private string _consistencyWarning = "";
    public string ConsistencyWarning { get => _consistencyWarning; private set => SetProperty(ref _consistencyWarning, value); }

    // ── Form state ────────────────────────────────────────────────────────────

    private bool _isFormValid;
    public bool IsFormValid { get => _isFormValid; private set => SetProperty(ref _isFormValid, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand SaveCommand             { get; }
    public ICommand AutoCalcDistanceCommand { get; }
    public ICommand AutoCalcSpeedCommand    { get; }
    public ICommand AutoCalcDurationCommand { get; }
    public ICommand ClearFormCommand        { get; }

    // ── Constructor ───────────────────────────────────────────────────────────

    public LogWorkoutViewModel(IWorkoutRepository repo, IUserSettingsRepository settingsRepo)
    {
        _repo         = repo         ?? throw new ArgumentNullException(nameof(repo));
        _settingsRepo = settingsRepo ?? throw new ArgumentNullException(nameof(settingsRepo));

        SaveCommand = new RelayCommand(
            async () => await SaveAsync(),
            () => _isFormValid && !_isBusy);

        AutoCalcDistanceCommand = new RelayCommand(
            AutoCalcDistance,
            () => _avgSpeedKmh > 0 && _durationSeconds > 0);

        AutoCalcSpeedCommand = new RelayCommand(
            AutoCalcSpeed,
            () => _distanceKm > 0 && _durationSeconds > 0);

        AutoCalcDurationCommand = new RelayCommand(
            AutoCalcDuration,
            () => _distanceKm > 0 && _avgSpeedKmh > 0);

        ClearFormCommand = new RelayCommand(ClearForm);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task LoadSettingsAsync()
    {
        var settings  = await _settingsRepo.GetAsync();
        _selectedUnit = settings.DistanceUnit;
        OnPropertyChanged(nameof(SelectedUnit));
    }

    /// <summary>Populates form fields from an existing session for editing.</summary>
    public void LoadSession(WorkoutSession session)
    {
        _editingSessionId = session.Id;
        SessionDate       = session.SessionDate;

        int h = session.DurationSeconds / 3600;
        int m = (session.DurationSeconds % 3600) / 60;
        int s = session.DurationSeconds % 60;

        // Set duration fields directly without triggering intermediate validation
        _durationHoursInput   = h.ToString();
        _durationMinutesInput = m.ToString();
        _durationSecondsInput = s.ToString();
        OnPropertyChanged(nameof(DurationHoursInput));
        OnPropertyChanged(nameof(DurationMinutesInput));
        OnPropertyChanged(nameof(DurationSecondsInput));

        // Set display-unit values
        _distanceInput = _selectedUnit == "mi"
            ? UnitConverter.KmToMiles(session.DistanceKm).ToString("F2", CultureInfo.InvariantCulture)
            : session.DistanceKm.ToString("F2", CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(DistanceInput));

        _speedInput = _selectedUnit == "mi"
            ? UnitConverter.KmhToMph(session.AvgSpeedKmh).ToString("F1", CultureInfo.InvariantCulture)
            : session.AvgSpeedKmh.ToString("F1", CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(SpeedInput));

        _inclineInput    = session.InclinePercent.ToString("F1", CultureInfo.InvariantCulture);
        _caloriesInput   = session.CaloriesBurned?.ToString() ?? "";
        _heartRateInput  = session.AvgHeartRate?.ToString()   ?? "";
        OnPropertyChanged(nameof(InclineInput));
        OnPropertyChanged(nameof(CaloriesInput));
        OnPropertyChanged(nameof(HeartRateInput));
        OnPropertyChanged(nameof(IsEditing));

        ValidateAll();
    }

    /// <summary>Exposed for unit tests so tests can await the async save directly.</summary>
    public async Task SaveAsync()
    {
        if (!_isFormValid || _isBusy) return;
        IsBusy = true;

        try
        {
            var session = new WorkoutSession
            {
                Id              = _editingSessionId ?? 0,
                SessionDate     = SessionDate,
                DurationSeconds = _durationSeconds,
                DistanceKm      = _distanceKm,
                AvgSpeedKmh     = _avgSpeedKmh,
                InclinePercent  = _inclinePercent,
                CaloriesBurned  = _calories,
                AvgHeartRate    = _heartRate
            };

            if (_editingSessionId.HasValue)
                await _repo.UpdateAsync(session);
            else
                await _repo.AddAsync(session);

            ClearForm();
            SaveSucceeded?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Parsing & validation ──────────────────────────────────────────────────

    private void ParseAndValidateDistance()
    {
        if (string.IsNullOrWhiteSpace(_distanceInput))
        {
            _distanceKm = 0;
            DistanceError = "Distance is required.";
        }
        else if (!double.TryParse(_distanceInput, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
        {
            _distanceKm = 0;
            DistanceError = "Distance must be a valid number.";
        }
        else
        {
            double km = _selectedUnit == "mi" ? UnitConverter.MilesToKm(val) : val;
            if (!WorkoutValidator.IsDistanceValid(km))
            {
                _distanceKm = 0;
                DistanceError = "Distance must be greater than 0.";
            }
            else
            {
                _distanceKm = km;
                DistanceError = "";
            }
        }
        UpdateFormState();
    }

    private void ParseAndValidateSpeed()
    {
        if (string.IsNullOrWhiteSpace(_speedInput))
        {
            _avgSpeedKmh = 0;
            SpeedError = "Average speed is required.";
        }
        else if (!double.TryParse(_speedInput, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
        {
            _avgSpeedKmh = 0;
            SpeedError = "Speed must be a valid number.";
        }
        else
        {
            double kmh = _selectedUnit == "mi" ? UnitConverter.MphToKmh(val) : val;
            if (!WorkoutValidator.IsSpeedValid(kmh))
            {
                _avgSpeedKmh = 0;
                SpeedError = $"Speed must be between 0 and {WorkoutValidator.MaxTreadmillSpeedKmh} km/h ({UnitConverter.KmhToMph(WorkoutValidator.MaxTreadmillSpeedKmh):F0} mph).";
            }
            else
            {
                _avgSpeedKmh = kmh;
                SpeedError = "";
            }
        }
        UpdateFormState();
    }

    private void ParseAndValidateDuration()
    {
        bool hOk = int.TryParse(_durationHoursInput,   out int h) && h >= 0;
        bool mOk = int.TryParse(_durationMinutesInput, out int m) && m is >= 0 and <= 59;
        bool sOk = int.TryParse(_durationSecondsInput, out int s) && s is >= 0 and <= 59;

        if (!hOk || !mOk || !sOk)
        {
            _durationSeconds = 0;
            DurationError = "Enter valid hours (≥ 0), minutes (0–59), and seconds (0–59).";
        }
        else
        {
            int total = h * 3600 + m * 60 + s;
            if (!WorkoutValidator.IsDurationValid(total))
            {
                _durationSeconds = 0;
                DurationError = "Duration must be at least 1 second.";
            }
            else
            {
                _durationSeconds = total;
                DurationError = "";
            }
        }
        UpdateFormState();
    }

    private void ParseAndValidateIncline()
    {
        if (!double.TryParse(_inclineInput, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
        {
            _inclinePercent = 0;
            InclineError = "Incline must be a valid number.";
        }
        else if (!WorkoutValidator.IsInclineValid(val))
        {
            _inclinePercent = 0;
            InclineError = $"Incline must be between 0 and {WorkoutValidator.MaxInclinePercent} %.";
        }
        else
        {
            _inclinePercent = val;
            InclineError = "";
        }
        UpdateFormState();
    }

    private void ParseAndValidateCalories()
    {
        if (string.IsNullOrWhiteSpace(_caloriesInput))
        {
            _calories     = null;
            CaloriesError = "";
        }
        else if (!int.TryParse(_caloriesInput, out int val))
        {
            _calories     = null;
            CaloriesError = "Calories must be a whole number.";
        }
        else if (!WorkoutValidator.IsCaloriesValid(val))
        {
            _calories     = null;
            CaloriesError = "Calories must be a positive number.";
        }
        else
        {
            _calories     = val;
            CaloriesError = "";
        }
        UpdateFormState();
    }

    private void ParseAndValidateHeartRate()
    {
        if (string.IsNullOrWhiteSpace(_heartRateInput))
        {
            _heartRate     = null;
            HeartRateError = "";
        }
        else if (!int.TryParse(_heartRateInput, out int val))
        {
            _heartRate     = null;
            HeartRateError = "Heart rate must be a whole number.";
        }
        else if (!WorkoutValidator.IsHeartRateValid(val))
        {
            _heartRate     = null;
            HeartRateError = $"Heart rate must be between {WorkoutValidator.MinHeartRateBpm} and {WorkoutValidator.MaxHeartRateBpm} BPM.";
        }
        else
        {
            _heartRate     = val;
            HeartRateError = "";
        }
        UpdateFormState();
    }

    private void ValidateAll()
    {
        ParseAndValidateDistance();
        ParseAndValidateSpeed();
        ParseAndValidateDuration();
        ParseAndValidateIncline();
        ParseAndValidateCalories();
        ParseAndValidateHeartRate();
    }

    private void UpdateFormState()
    {
        bool valid = string.IsNullOrEmpty(_distanceError)
                  && string.IsNullOrEmpty(_speedError)
                  && string.IsNullOrEmpty(_durationError)
                  && string.IsNullOrEmpty(_inclineError)
                  && string.IsNullOrEmpty(_caloriesError)
                  && string.IsNullOrEmpty(_heartRateError);

        // Consistency is a WARNING — it does not block saving
        ConsistencyWarning = valid && _distanceKm > 0 && _avgSpeedKmh > 0 && _durationSeconds > 0
                          && !WorkoutValidator.IsPhysicallyConsistent(_distanceKm, _avgSpeedKmh, _durationSeconds)
            ? $"Warning: these values are not physically consistent. Expected ~{WorkoutValidator.CalculateDistance(_avgSpeedKmh, _durationSeconds):F2} km."
            : "";

        IsFormValid = valid;
        (_saveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        RaiseAutoCalcCanExecuteChanged();
    }

    // ── Auto-calculation ──────────────────────────────────────────────────────

    private void AutoCalcDistance()
    {
        double km = WorkoutValidator.CalculateDistance(_avgSpeedKmh, _durationSeconds);
        DistanceInput = (_selectedUnit == "mi"
            ? UnitConverter.KmToMiles(km)
            : km).ToString("F2", CultureInfo.InvariantCulture);
    }

    private void AutoCalcSpeed()
    {
        double kmh = WorkoutValidator.CalculateSpeed(_distanceKm, _durationSeconds);
        SpeedInput = (_selectedUnit == "mi"
            ? UnitConverter.KmhToMph(kmh)
            : kmh).ToString("F1", CultureInfo.InvariantCulture);
    }

    private void AutoCalcDuration()
    {
        int total = WorkoutValidator.CalculateDuration(_distanceKm, _avgSpeedKmh);
        _durationHoursInput   = (total / 3600).ToString();
        _durationMinutesInput = ((total % 3600) / 60).ToString();
        _durationSecondsInput = (total % 60).ToString();
        OnPropertyChanged(nameof(DurationHoursInput));
        OnPropertyChanged(nameof(DurationMinutesInput));
        OnPropertyChanged(nameof(DurationSecondsInput));
        ParseAndValidateDuration();
    }

    // ── Unit switching ────────────────────────────────────────────────────────

    private void ConvertDisplayValuesOnUnitChange(string oldUnit, string newUnit)
    {
        // Re-express the current metric backing values in the new unit
        if (_distanceKm > 0)
        {
            double display = newUnit == "mi" ? UnitConverter.KmToMiles(_distanceKm) : _distanceKm;
            _distanceInput = display.ToString("F2", CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(DistanceInput));
        }

        if (_avgSpeedKmh > 0)
        {
            double display = newUnit == "mi" ? UnitConverter.KmhToMph(_avgSpeedKmh) : _avgSpeedKmh;
            _speedInput = display.ToString("F1", CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(SpeedInput));
        }

        // Re-validate with new unit context (backing values unchanged; errors re-evaluated)
        ValidateAll();
    }

    // ── Form reset ────────────────────────────────────────────────────────────

    public void ClearForm()
    {
        _editingSessionId     = null;
        _distanceKm           = 0;
        _avgSpeedKmh          = 0;
        _durationSeconds      = 0;
        _inclinePercent       = 0;
        _calories             = null;
        _heartRate            = null;

        _distanceInput        = "";
        _speedInput           = "";
        _durationHoursInput   = "0";
        _durationMinutesInput = "0";
        _durationSecondsInput = "0";
        _inclineInput         = "0";
        _caloriesInput        = "";
        _heartRateInput       = "";
        _sessionDate          = DateTime.Now;

        OnPropertyChanged(nameof(DistanceInput));
        OnPropertyChanged(nameof(SpeedInput));
        OnPropertyChanged(nameof(DurationHoursInput));
        OnPropertyChanged(nameof(DurationMinutesInput));
        OnPropertyChanged(nameof(DurationSecondsInput));
        OnPropertyChanged(nameof(InclineInput));
        OnPropertyChanged(nameof(CaloriesInput));
        OnPropertyChanged(nameof(HeartRateInput));
        OnPropertyChanged(nameof(SessionDate));
        OnPropertyChanged(nameof(IsEditing));

        DistanceError     = "";
        SpeedError        = "";
        DurationError     = "";
        InclineError      = "";
        CaloriesError     = "";
        HeartRateError    = "";
        ConsistencyWarning = "";
        IsFormValid       = false;
        (_saveCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    // Expose _saveCommand as the backing field to avoid cast noise
    private ICommand? _saveCommand => SaveCommand;

    private void RaiseAutoCalcCanExecuteChanged()
    {
        (AutoCalcDistanceCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AutoCalcSpeedCommand    as RelayCommand)?.RaiseCanExecuteChanged();
        (AutoCalcDurationCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}
