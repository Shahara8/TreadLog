using Moq;
using TreadLog.Models;
using TreadLog.Services.Interfaces;
using TreadLog.ViewModels;
using TreadLog.ViewModels.Support;
using Xunit;

namespace TreadLog.Tests.ViewModels;

public class DashboardViewModelTests
{
    private static UserSettings KmSettings()  => new() { Id = 1, DistanceUnit = "km" };
    private static UserSettings MiSettings()  => new() { Id = 1, DistanceUnit = "mi" };

    private static WorkoutSession Session(double distKm, double speedKmh, int durSec,
        double incline = 0, DateTime? date = null)
        => new()
        {
            Id              = 1,
            SessionDate     = date ?? DateTime.UtcNow,
            DistanceKm      = distKm,
            AvgSpeedKmh     = speedKmh,
            DurationSeconds = durSec,
            InclinePercent  = incline
        };

    // ── Empty dataset ─────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadDataAsync_NoSessions_ShowsZeroKpis()
    {
        var workoutRepo  = new Mock<IWorkoutRepository>();
        var settingsRepo = new Mock<IUserSettingsRepository>();
        settingsRepo.Setup(r => r.GetAsync()).ReturnsAsync(KmSettings());
        workoutRepo.Setup(r => r.GetByDateRangeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Enumerable.Empty<WorkoutSession>());

        var vm = new DashboardViewModel(workoutRepo.Object, settingsRepo.Object);
        await vm.LoadDataAsync();

        Assert.Equal(0, vm.SessionCount);
        Assert.Equal("0.0 km", vm.TotalDistanceDisplay);
        Assert.Equal("0 h, 0 min", vm.TotalTimeDisplay);
        Assert.Equal("--:--", vm.AveragePaceDisplay);
    }

    [Fact]
    public async Task LoadDataAsync_NoSessions_MiUnit_ShowsZeroMi()
    {
        var workoutRepo  = new Mock<IWorkoutRepository>();
        var settingsRepo = new Mock<IUserSettingsRepository>();
        settingsRepo.Setup(r => r.GetAsync()).ReturnsAsync(MiSettings());
        workoutRepo.Setup(r => r.GetByDateRangeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Enumerable.Empty<WorkoutSession>());

        var vm = new DashboardViewModel(workoutRepo.Object, settingsRepo.Object);
        await vm.LoadDataAsync();

        Assert.Contains("0.0 mi", vm.TotalDistanceDisplay);
    }

    // ── KPI calculation ───────────────────────────────────────────────────────

    [Fact]
    public async Task LoadDataAsync_SingleSession_CalculatesKpis()
    {
        // 5 km at constant speed for 30 minutes = 10 km/h
        var session = Session(distKm: 5.0, speedKmh: 10.0, durSec: 1800);

        var workoutRepo  = new Mock<IWorkoutRepository>();
        var settingsRepo = new Mock<IUserSettingsRepository>();
        settingsRepo.Setup(r => r.GetAsync()).ReturnsAsync(KmSettings());
        workoutRepo.Setup(r => r.GetByDateRangeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new[] { session });

        var vm = new DashboardViewModel(workoutRepo.Object, settingsRepo.Object);
        await vm.LoadDataAsync();

        Assert.Equal(1, vm.SessionCount);
        Assert.Equal("5.0 km", vm.TotalDistanceDisplay);
        Assert.Equal("0 h, 30 min", vm.TotalTimeDisplay);
        Assert.NotEqual("--:--", vm.AveragePaceDisplay);
    }

    [Fact]
    public async Task LoadDataAsync_MultipleSessions_SumsDistance()
    {
        var sessions = new[]
        {
            Session(distKm: 3.0, speedKmh: 6.0, durSec: 1800),
            Session(distKm: 7.0, speedKmh: 7.0, durSec: 3600)
        };

        var workoutRepo  = new Mock<IWorkoutRepository>();
        var settingsRepo = new Mock<IUserSettingsRepository>();
        settingsRepo.Setup(r => r.GetAsync()).ReturnsAsync(KmSettings());
        workoutRepo.Setup(r => r.GetByDateRangeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(sessions);

        var vm = new DashboardViewModel(workoutRepo.Object, settingsRepo.Object);
        await vm.LoadDataAsync();

        Assert.Equal(2, vm.SessionCount);
        Assert.Equal("10.0 km", vm.TotalDistanceDisplay);
        Assert.Equal("1 h, 30 min", vm.TotalTimeDisplay);
    }

    [Fact]
    public async Task LoadDataAsync_MiUnit_ConvertsDistance()
    {
        var session = Session(distKm: 1.60934, speedKmh: 9.656, durSec: 600);

        var workoutRepo  = new Mock<IWorkoutRepository>();
        var settingsRepo = new Mock<IUserSettingsRepository>();
        settingsRepo.Setup(r => r.GetAsync()).ReturnsAsync(MiSettings());
        workoutRepo.Setup(r => r.GetByDateRangeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new[] { session });

        var vm = new DashboardViewModel(workoutRepo.Object, settingsRepo.Object);
        await vm.LoadDataAsync();

        Assert.Contains("mi", vm.TotalDistanceDisplay);
        Assert.StartsWith("1.0", vm.TotalDistanceDisplay);
    }

    // ── IsLoading ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadDataAsync_SetsIsLoading_ThenClearsIt()
    {
        var workoutRepo  = new Mock<IWorkoutRepository>();
        var settingsRepo = new Mock<IUserSettingsRepository>();
        settingsRepo.Setup(r => r.GetAsync()).ReturnsAsync(KmSettings());
        workoutRepo.Setup(r => r.GetByDateRangeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Enumerable.Empty<WorkoutSession>());

        var vm = new DashboardViewModel(workoutRepo.Object, settingsRepo.Object);
        bool wasLoadingDuringCall = false;
        settingsRepo.Setup(r => r.GetAsync()).ReturnsAsync(() =>
        {
            wasLoadingDuringCall = vm.IsLoading;
            return KmSettings();
        });

        await vm.LoadDataAsync();

        Assert.True(wasLoadingDuringCall);
        Assert.False(vm.IsLoading);
    }

    // ── Date filter ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SelectedFilter_NonCustom_TriggersReload()
    {
        var workoutRepo  = new Mock<IWorkoutRepository>();
        var settingsRepo = new Mock<IUserSettingsRepository>();
        settingsRepo.Setup(r => r.GetAsync()).ReturnsAsync(KmSettings());
        workoutRepo.Setup(r => r.GetByDateRangeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Enumerable.Empty<WorkoutSession>());

        var vm = new DashboardViewModel(workoutRepo.Object, settingsRepo.Object);

        // Load once manually first
        await vm.LoadDataAsync();
        int callsBefore = workoutRepo.Invocations.Count(i => i.Method.Name == "GetByDateRangeAsync");

        vm.SelectedFilter = DateFilterOption.Last7Days;
        await Task.Delay(50); // allow async fire-and-forget to start

        int callsAfter = workoutRepo.Invocations.Count(i => i.Method.Name == "GetByDateRangeAsync");
        Assert.True(callsAfter > callsBefore);
    }

    [Fact]
    public void ApplyCustomFilterCommand_DisabledWithoutDates()
    {
        var workoutRepo  = new Mock<IWorkoutRepository>();
        var settingsRepo = new Mock<IUserSettingsRepository>();
        settingsRepo.Setup(r => r.GetAsync()).ReturnsAsync(KmSettings());

        var vm = new DashboardViewModel(workoutRepo.Object, settingsRepo.Object);
        vm.SelectedFilter = DateFilterOption.Custom;

        Assert.False(vm.ApplyCustomFilterCommand.CanExecute(null));
    }

    [Fact]
    public void ApplyCustomFilterCommand_EnabledWithValidDates()
    {
        var workoutRepo  = new Mock<IWorkoutRepository>();
        var settingsRepo = new Mock<IUserSettingsRepository>();
        settingsRepo.Setup(r => r.GetAsync()).ReturnsAsync(KmSettings());

        var vm = new DashboardViewModel(workoutRepo.Object, settingsRepo.Object);
        vm.SelectedFilter  = DateFilterOption.Custom;
        vm.CustomStartDate = DateTime.Today.AddDays(-7);
        vm.CustomEndDate   = DateTime.Today;

        Assert.True(vm.ApplyCustomFilterCommand.CanExecute(null));
    }

    [Fact]
    public void ApplyCustomFilterCommand_DisabledWhenEndBeforeStart()
    {
        var workoutRepo  = new Mock<IWorkoutRepository>();
        var settingsRepo = new Mock<IUserSettingsRepository>();

        var vm = new DashboardViewModel(workoutRepo.Object, settingsRepo.Object);
        vm.SelectedFilter  = DateFilterOption.Custom;
        vm.CustomStartDate = DateTime.Today;
        vm.CustomEndDate   = DateTime.Today.AddDays(-1);

        Assert.False(vm.ApplyCustomFilterCommand.CanExecute(null));
    }

    // ── Chart data ────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadDataAsync_BuildsWeeklyDistancePoints()
    {
        var monday = DateTime.UtcNow.Date;
        while (monday.DayOfWeek != DayOfWeek.Monday) monday = monday.AddDays(-1);

        var sessions = new[]
        {
            Session(distKm: 3.0, speedKmh: 6.0, durSec: 1800, date: monday),
            Session(distKm: 4.0, speedKmh: 8.0, durSec: 1800, date: monday.AddDays(1))
        };

        var workoutRepo  = new Mock<IWorkoutRepository>();
        var settingsRepo = new Mock<IUserSettingsRepository>();
        settingsRepo.Setup(r => r.GetAsync()).ReturnsAsync(KmSettings());
        workoutRepo.Setup(r => r.GetByDateRangeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(sessions);

        var vm = new DashboardViewModel(workoutRepo.Object, settingsRepo.Object);
        await vm.LoadDataAsync();

        // Both sessions are in the same week, so one bar totalling 7 km
        Assert.Single(vm.WeeklyDistancePoints);
        Assert.Equal(7.0, vm.WeeklyDistancePoints[0].Value, precision: 2);
    }

    [Fact]
    public async Task LoadDataAsync_BuildsSpeedAndInclineTrendPoints()
    {
        var sessions = new[]
        {
            Session(distKm: 3.0, speedKmh: 8.0, durSec: 1350, incline: 1.0),
            Session(distKm: 5.0, speedKmh: 10.0, durSec: 1800, incline: 2.5,
                date: DateTime.UtcNow.AddDays(-1))
        };

        var workoutRepo  = new Mock<IWorkoutRepository>();
        var settingsRepo = new Mock<IUserSettingsRepository>();
        settingsRepo.Setup(r => r.GetAsync()).ReturnsAsync(KmSettings());
        workoutRepo.Setup(r => r.GetByDateRangeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(sessions);

        var vm = new DashboardViewModel(workoutRepo.Object, settingsRepo.Object);
        await vm.LoadDataAsync();

        Assert.Equal(2, vm.SpeedTrendPoints.Count);
        Assert.Equal(2, vm.InclineTrendPoints.Count);
    }

    // ── Constructor guard ─────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullWorkoutRepo_Throws()
    {
        var settingsRepo = new Mock<IUserSettingsRepository>();
        Assert.Throws<ArgumentNullException>(() =>
            new DashboardViewModel(null!, settingsRepo.Object));
    }

    [Fact]
    public void Constructor_NullSettingsRepo_Throws()
    {
        var workoutRepo = new Mock<IWorkoutRepository>();
        Assert.Throws<ArgumentNullException>(() =>
            new DashboardViewModel(workoutRepo.Object, null!));
    }
}
