using Moq;
using TreadLog.Models;
using TreadLog.Services.Interfaces;
using TreadLog.ViewModels;
using Xunit;

namespace TreadLog.Tests.ViewModels;

public class HistoryViewModelTests
{
    private static UserSettings KmSettings() =>
        new() { Id = 1, DistanceUnit = "km" };

    private static WorkoutSession MakeSession(int id, double distKm = 5.0, DateTime? date = null) =>
        new()
        {
            Id              = id,
            SessionDate     = date ?? DateTime.UtcNow,
            DistanceKm      = distKm,
            AvgSpeedKmh     = 10.0,
            DurationSeconds = 1800,
            InclinePercent  = 0
        };

    private static (HistoryViewModel vm, Mock<IWorkoutRepository> repo) Build(
        IEnumerable<WorkoutSession>? sessions = null)
    {
        var settingsRepo = new Mock<IUserSettingsRepository>();
        settingsRepo.Setup(r => r.GetAsync()).ReturnsAsync(KmSettings());

        var workoutRepo = new Mock<IWorkoutRepository>();
        workoutRepo.Setup(r => r.GetAllAsync())
            .ReturnsAsync(sessions ?? Enumerable.Empty<WorkoutSession>());
        workoutRepo.Setup(r => r.GetByDateRangeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(sessions ?? Enumerable.Empty<WorkoutSession>());
        workoutRepo.Setup(r => r.DeleteAsync(It.IsAny<int>())).Returns(Task.CompletedTask);

        var vm = new HistoryViewModel(workoutRepo.Object, settingsRepo.Object);
        return (vm, workoutRepo);
    }

    // ── LoadDataAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadDataAsync_NoSessions_SessionsCollectionEmpty()
    {
        var (vm, _) = Build();
        await vm.LoadDataAsync();
        Assert.Empty(vm.Sessions);
    }

    [Fact]
    public async Task LoadDataAsync_TwoSessions_SessionsCollectionHasTwo()
    {
        var sessions = new[] { MakeSession(1), MakeSession(2) };
        var (vm, _) = Build(sessions);

        await vm.LoadDataAsync();

        Assert.Equal(2, vm.Sessions.Count);
    }

    [Fact]
    public async Task LoadDataAsync_SetsIsLoading_ThenClears()
    {
        bool wasLoading = false;
        var settingsRepo = new Mock<IUserSettingsRepository>();
        settingsRepo.Setup(r => r.GetAsync()).ReturnsAsync(() =>
        {
            wasLoading = true; // captured inside the async call
            return KmSettings();
        });
        var workoutRepo = new Mock<IWorkoutRepository>();
        workoutRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(Enumerable.Empty<WorkoutSession>());

        var vm = new HistoryViewModel(workoutRepo.Object, settingsRepo.Object);
        await vm.LoadDataAsync();

        Assert.True(wasLoading);
        Assert.False(vm.IsLoading);
    }

    // ── SearchText filter ─────────────────────────────────────────────────────

    [Fact]
    public async Task SearchText_FiltersSessionsByDateDisplay()
    {
        // Create sessions on specific dates so we can filter by year substring
        var jan = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var jun = new DateTime(2024, 6, 20, 0, 0, 0, DateTimeKind.Utc);

        var sessions = new[] { MakeSession(1, date: jan), MakeSession(2, date: jun) };
        var (vm, _) = Build(sessions);
        await vm.LoadDataAsync();

        vm.SearchText = "Jan";

        Assert.Single(vm.Sessions);
    }

    [Fact]
    public async Task SearchText_Empty_ShowsAllSessions()
    {
        var sessions = new[] { MakeSession(1), MakeSession(2), MakeSession(3) };
        var (vm, _) = Build(sessions);
        await vm.LoadDataAsync();

        vm.SearchText = "xyz";
        Assert.Empty(vm.Sessions);

        vm.SearchText = "";
        Assert.Equal(3, vm.Sessions.Count);
    }

    // ── SelectedSession ───────────────────────────────────────────────────────

    [Fact]
    public async Task SelectedSession_EnablesEditAndDeleteCommands()
    {
        var (vm, _) = Build(new[] { MakeSession(1) });
        await vm.LoadDataAsync();

        vm.SelectedSession = vm.Sessions[0];

        Assert.True(vm.EditCommand.CanExecute(null));
        Assert.True(vm.DeleteCommand.CanExecute(null));
    }

    [Fact]
    public void SelectedSession_Null_DisablesEditAndDeleteCommands()
    {
        var (vm, _) = Build();
        vm.SelectedSession = null;

        Assert.False(vm.EditCommand.CanExecute(null));
        Assert.False(vm.DeleteCommand.CanExecute(null));
    }

    // ── EditCommand ───────────────────────────────────────────────────────────

    [Fact]
    public async Task EditCommand_RaisesEditRequestedWithCorrectSession()
    {
        var session = MakeSession(7);
        var (vm, _) = Build(new[] { session });
        await vm.LoadDataAsync();
        vm.SelectedSession = vm.Sessions[0];

        WorkoutSession? received = null;
        vm.EditRequested += (_, s) => received = s;

        vm.EditCommand.Execute(null);

        Assert.NotNull(received);
        Assert.Equal(7, received!.Id);
    }

    // ── DeleteSelectedAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task DeleteSelectedAsync_RemovesSessionFromCollection()
    {
        var sessions = new[] { MakeSession(1), MakeSession(2) };
        var (vm, repo) = Build(sessions);
        await vm.LoadDataAsync();
        vm.SelectedSession = vm.Sessions[0];

        await vm.DeleteSelectedAsync();

        Assert.Single(vm.Sessions);
        repo.Verify(r => r.DeleteAsync(1), Times.Once);
    }

    [Fact]
    public async Task DeleteSelectedAsync_ClearsSelectedSession()
    {
        var (vm, _) = Build(new[] { MakeSession(1) });
        await vm.LoadDataAsync();
        vm.SelectedSession = vm.Sessions[0];

        await vm.DeleteSelectedAsync();

        Assert.Null(vm.SelectedSession);
    }

    [Fact]
    public async Task DeleteSelectedAsync_WhenNoneSelected_DoesNothing()
    {
        var (vm, repo) = Build();
        await vm.LoadDataAsync();
        vm.SelectedSession = null;

        await vm.DeleteSelectedAsync();

        repo.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
    }

    // ── ClearFilterCommand ────────────────────────────────────────────────────

    [Fact]
    public async Task ClearFilterCommand_ResetsFilterDates()
    {
        var (vm, _) = Build();
        vm.FilterStartDate = DateTime.Today.AddDays(-7);
        vm.FilterEndDate   = DateTime.Today;

        vm.ClearFilterCommand.Execute(null);

        Assert.Null(vm.FilterStartDate);
        Assert.Null(vm.FilterEndDate);
    }

    // ── Constructor guard ─────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullWorkoutRepo_Throws()
    {
        var settings = new Mock<IUserSettingsRepository>();
        Assert.Throws<ArgumentNullException>(() => new HistoryViewModel(null!, settings.Object));
    }

    [Fact]
    public void Constructor_NullSettingsRepo_Throws()
    {
        var repo = new Mock<IWorkoutRepository>();
        Assert.Throws<ArgumentNullException>(() => new HistoryViewModel(repo.Object, null!));
    }
}
