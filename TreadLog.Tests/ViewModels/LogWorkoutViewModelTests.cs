using Moq;
using TreadLog.Models;
using TreadLog.Services.Interfaces;
using TreadLog.ViewModels;
using Xunit;

namespace TreadLog.Tests.ViewModels;

public class LogWorkoutViewModelTests
{
    private static LogWorkoutViewModel Build(
        IWorkoutRepository? repo = null,
        IUserSettingsRepository? settings = null,
        string unit = "km")
    {
        var workoutRepo  = repo     ?? MockRepo().Object;
        var settingsRepo = settings ?? MockSettings(unit).Object;
        return new LogWorkoutViewModel(workoutRepo, settingsRepo);
    }

    private static Mock<IWorkoutRepository> MockRepo()
    {
        var m = new Mock<IWorkoutRepository>();
        m.Setup(r => r.AddAsync(It.IsAny<WorkoutSession>())).ReturnsAsync(1);
        m.Setup(r => r.UpdateAsync(It.IsAny<WorkoutSession>())).Returns(Task.CompletedTask);
        return m;
    }

    private static Mock<IUserSettingsRepository> MockSettings(string unit = "km")
    {
        var m = new Mock<IUserSettingsRepository>();
        m.Setup(r => r.GetAsync()).ReturnsAsync(new UserSettings { Id = 1, DistanceUnit = unit });
        return m;
    }

    // ── Initial state ─────────────────────────────────────────────────────────

    [Fact]
    public void InitialState_IsFormValid_False()
    {
        var vm = Build();
        Assert.False(vm.IsFormValid);
    }

    [Fact]
    public void InitialState_SaveCommand_CannotExecute()
    {
        var vm = Build();
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void InitialState_IsEditing_False()
    {
        var vm = Build();
        Assert.False(vm.IsEditing);
    }

    // ── Validation — distance ─────────────────────────────────────────────────

    [Fact]
    public void DistanceInput_Empty_SetsDistanceError()
    {
        var vm = Build();
        vm.DistanceInput = "";
        Assert.NotEmpty(vm.DistanceError);
    }

    [Fact]
    public void DistanceInput_NotANumber_SetsDistanceError()
    {
        var vm = Build();
        vm.DistanceInput = "abc";
        Assert.NotEmpty(vm.DistanceError);
    }

    [Fact]
    public void DistanceInput_Negative_SetsDistanceError()
    {
        var vm = Build();
        vm.DistanceInput = "-1";
        Assert.NotEmpty(vm.DistanceError);
    }

    [Fact]
    public void DistanceInput_Valid_ClearsDistanceError()
    {
        var vm = Build();
        vm.DistanceInput = "5.0";
        Assert.Empty(vm.DistanceError);
    }

    // ── Validation — speed ────────────────────────────────────────────────────

    [Fact]
    public void SpeedInput_Empty_SetsSpeedError()
    {
        var vm = Build();
        vm.SpeedInput = "";
        Assert.NotEmpty(vm.SpeedError);
    }

    [Fact]
    public void SpeedInput_ExceedsMax_SetsSpeedError()
    {
        var vm = Build();
        vm.SpeedInput = "99";
        Assert.NotEmpty(vm.SpeedError);
    }

    [Fact]
    public void SpeedInput_Valid_ClearsSpeedError()
    {
        var vm = Build();
        vm.SpeedInput = "10.0";
        Assert.Empty(vm.SpeedError);
    }

    // ── Validation — duration ─────────────────────────────────────────────────

    [Fact]
    public void DurationInputs_AllZero_SetsDurationError()
    {
        var vm = Build();
        vm.DurationHoursInput   = "0";
        vm.DurationMinutesInput = "0";
        vm.DurationSecondsInput = "0";
        Assert.NotEmpty(vm.DurationError);
    }

    [Fact]
    public void DurationInputs_Valid_ClearsDurationError()
    {
        var vm = Build();
        vm.DurationHoursInput   = "0";
        vm.DurationMinutesInput = "30";
        vm.DurationSecondsInput = "0";
        Assert.Empty(vm.DurationError);
    }

    [Fact]
    public void DurationInputs_InvalidMinutes_SetsDurationError()
    {
        var vm = Build();
        vm.DurationMinutesInput = "60"; // out of range
        Assert.NotEmpty(vm.DurationError);
    }

    // ── Validation — incline ──────────────────────────────────────────────────

    [Fact]
    public void InclineInput_ExceedsMax_SetsInclineError()
    {
        var vm = Build();
        vm.InclineInput = "16";
        Assert.NotEmpty(vm.InclineError);
    }

    [Fact]
    public void InclineInput_Zero_ClearsInclineError()
    {
        var vm = Build();
        vm.InclineInput = "0";
        Assert.Empty(vm.InclineError);
    }

    // ── Validation — optional fields ──────────────────────────────────────────

    [Fact]
    public void CaloriesInput_Negative_SetsCaloriesError()
    {
        var vm = Build();
        vm.CaloriesInput = "-50";
        Assert.NotEmpty(vm.CaloriesError);
    }

    [Fact]
    public void CaloriesInput_Empty_IsValid()
    {
        var vm = Build();
        vm.CaloriesInput = "";
        Assert.Empty(vm.CaloriesError);
    }

    [Fact]
    public void HeartRateInput_OutOfRange_SetsHeartRateError()
    {
        var vm = Build();
        vm.HeartRateInput = "300";
        Assert.NotEmpty(vm.HeartRateError);
    }

    [Fact]
    public void HeartRateInput_Empty_IsValid()
    {
        var vm = Build();
        vm.HeartRateInput = "";
        Assert.Empty(vm.HeartRateError);
    }

    // ── IsFormValid ───────────────────────────────────────────────────────────

    private static void FillValidKmInputs(LogWorkoutViewModel vm)
    {
        vm.DistanceInput        = "5.0";
        vm.SpeedInput           = "10.0";
        vm.DurationHoursInput   = "0";
        vm.DurationMinutesInput = "30";
        vm.DurationSecondsInput = "0";
        vm.InclineInput         = "1.0";
    }

    [Fact]
    public void AllValidInputs_IsFormValid_True()
    {
        var vm = Build();
        FillValidKmInputs(vm);
        Assert.True(vm.IsFormValid);
    }

    [Fact]
    public void AllValidInputs_SaveCommand_CanExecute()
    {
        var vm = Build();
        FillValidKmInputs(vm);
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    // ── Consistency warning ───────────────────────────────────────────────────

    [Fact]
    public void InconsistentValues_SetsConsistencyWarning_DoesNotBlockSave()
    {
        var vm = Build();
        // 1 km at 10 km/h in 30 minutes — speed × time = 5 km ≠ 1 km
        vm.DistanceInput        = "1.0";
        vm.SpeedInput           = "10.0";
        vm.DurationHoursInput   = "0";
        vm.DurationMinutesInput = "30";
        vm.DurationSecondsInput = "0";
        vm.InclineInput         = "0";

        Assert.NotEmpty(vm.ConsistencyWarning);
        Assert.True(vm.IsFormValid); // warning does not block
    }

    // ── SaveAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_ValidForm_CallsAddAsync()
    {
        var repo = MockRepo();
        var vm   = Build(repo: repo.Object);
        FillValidKmInputs(vm);

        await vm.SaveAsync();

        repo.Verify(r => r.AddAsync(It.IsAny<WorkoutSession>()), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_ValidForm_RaisesSaveSucceeded()
    {
        var vm = Build();
        FillValidKmInputs(vm);

        bool raised = false;
        vm.SaveSucceeded += (_, _) => raised = true;

        await vm.SaveAsync();

        Assert.True(raised);
    }

    [Fact]
    public async Task SaveAsync_ClearsFormAfterSave()
    {
        var vm = Build();
        FillValidKmInputs(vm);

        await vm.SaveAsync();

        Assert.False(vm.IsFormValid);
        Assert.False(vm.IsEditing);
    }

    [Fact]
    public async Task SaveAsync_InvalidForm_DoesNotCallRepo()
    {
        var repo = MockRepo();
        var vm   = Build(repo: repo.Object);
        // Form is invalid by default (empty)

        await vm.SaveAsync();

        repo.Verify(r => r.AddAsync(It.IsAny<WorkoutSession>()), Times.Never);
    }

    // ── Edit mode ─────────────────────────────────────────────────────────────

    [Fact]
    public void LoadSession_SetsIsEditing()
    {
        var vm = Build();
        var session = new WorkoutSession
        {
            Id              = 5,
            SessionDate     = DateTime.UtcNow,
            DistanceKm      = 5.0,
            AvgSpeedKmh     = 10.0,
            DurationSeconds = 1800,
            InclinePercent  = 1.5
        };

        vm.LoadSession(session);

        Assert.True(vm.IsEditing);
    }

    [Fact]
    public void LoadSession_PopulatesDistanceInput()
    {
        var vm = Build();
        var session = new WorkoutSession
        {
            Id = 1, SessionDate = DateTime.UtcNow,
            DistanceKm = 8.5, AvgSpeedKmh = 10.0, DurationSeconds = 3060, InclinePercent = 0
        };

        vm.LoadSession(session);

        Assert.Equal("8.50", vm.DistanceInput);
    }

    [Fact]
    public async Task SaveAsync_InEditMode_CallsUpdateAsync()
    {
        var repo = MockRepo();
        var vm   = Build(repo: repo.Object);
        var session = new WorkoutSession
        {
            Id = 3, SessionDate = DateTime.UtcNow,
            DistanceKm = 5.0, AvgSpeedKmh = 10.0, DurationSeconds = 1800, InclinePercent = 0
        };
        vm.LoadSession(session);

        await vm.SaveAsync();

        repo.Verify(r => r.UpdateAsync(It.Is<WorkoutSession>(s => s.Id == 3)), Times.Once);
        repo.Verify(r => r.AddAsync(It.IsAny<WorkoutSession>()), Times.Never);
    }

    // ── Auto-calculation ──────────────────────────────────────────────────────

    [Fact]
    public void AutoCalcDistanceCommand_PopulatesDistanceInput()
    {
        var vm = Build();
        // 10 km/h for 30 min = 5 km
        vm.SpeedInput           = "10.0";
        vm.DurationHoursInput   = "0";
        vm.DurationMinutesInput = "30";
        vm.DurationSecondsInput = "0";
        vm.DistanceInput        = ""; // clear so we can check it gets set

        vm.AutoCalcDistanceCommand.Execute(null);

        Assert.NotEmpty(vm.DistanceInput);
        Assert.True(double.TryParse(vm.DistanceInput, out double d));
        Assert.Equal(5.0, d, precision: 1);
    }

    [Fact]
    public void AutoCalcSpeedCommand_PopulatesSpeedInput()
    {
        var vm = Build();
        // 5 km in 30 min = 10 km/h
        vm.DistanceInput        = "5.0";
        vm.DurationHoursInput   = "0";
        vm.DurationMinutesInput = "30";
        vm.DurationSecondsInput = "0";

        vm.AutoCalcSpeedCommand.Execute(null);

        Assert.NotEmpty(vm.SpeedInput);
        Assert.True(double.TryParse(vm.SpeedInput, out double spd));
        Assert.Equal(10.0, spd, precision: 1);
    }

    [Fact]
    public void AutoCalcDurationCommand_PopulatesDurationInputs()
    {
        var vm = Build();
        // 5 km at 10 km/h = 30 min
        vm.DistanceInput = "5.0";
        vm.SpeedInput    = "10.0";

        vm.AutoCalcDurationCommand.Execute(null);

        Assert.Equal("0", vm.DurationHoursInput);
        Assert.Equal("30", vm.DurationMinutesInput);
    }

    // ── Unit switching ────────────────────────────────────────────────────────

    [Fact]
    public void SwitchingUnit_Km_To_Mi_ConvertsDistanceDisplay()
    {
        var vm = Build();
        vm.DistanceInput = "1.60934"; // ≈ 1 mile
        vm.SpeedInput    = "10.0";
        vm.DurationHoursInput   = "0";
        vm.DurationMinutesInput = "9";
        vm.DurationSecondsInput = "39";
        vm.InclineInput  = "0";

        vm.SelectedUnit = "mi";

        // Display value should now be ~1.0 mi
        Assert.True(double.TryParse(vm.DistanceInput, out double d));
        Assert.Equal(1.0, d, precision: 1);
    }

    // ── ClearForm ─────────────────────────────────────────────────────────────

    [Fact]
    public void ClearForm_ResetsAllInputs()
    {
        var vm = Build();
        FillValidKmInputs(vm);
        Assert.True(vm.IsFormValid);

        vm.ClearForm();

        Assert.Empty(vm.DistanceInput);
        Assert.Empty(vm.SpeedInput);
        Assert.False(vm.IsFormValid);
        Assert.False(vm.IsEditing);
    }

    // ── LoadSettingsAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task LoadSettingsAsync_SetsMiUnit()
    {
        var vm = Build(unit: "mi");
        await vm.LoadSettingsAsync();
        Assert.Equal("mi", vm.SelectedUnit);
    }

    // ── Constructor guard ─────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullRepo_Throws()
    {
        var settings = MockSettings().Object;
        Assert.Throws<ArgumentNullException>(() => new LogWorkoutViewModel(null!, settings));
    }

    [Fact]
    public void Constructor_NullSettings_Throws()
    {
        var repo = MockRepo().Object;
        Assert.Throws<ArgumentNullException>(() => new LogWorkoutViewModel(repo, null!));
    }
}
