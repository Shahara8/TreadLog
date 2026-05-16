using Moq;
using TreadLog.Models;
using TreadLog.Services.Interfaces;
using TreadLog.ViewModels;
using Xunit;

namespace TreadLog.Tests.ViewModels;

public class MainViewModelTests
{
    private static (MainViewModel main, DashboardViewModel dash, LogWorkoutViewModel log,
                    HistoryViewModel hist, SettingsViewModel settings)
        BuildAll()
    {
        var workoutRepo  = new Mock<IWorkoutRepository>();
        var settingsRepo = new Mock<IUserSettingsRepository>();

        settingsRepo.Setup(r => r.GetAsync())
            .ReturnsAsync(new UserSettings { Id = 1, DistanceUnit = "km" });
        workoutRepo.Setup(r => r.GetAllAsync())
            .ReturnsAsync(Enumerable.Empty<WorkoutSession>());
        workoutRepo.Setup(r => r.GetByDateRangeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Enumerable.Empty<WorkoutSession>());

        var dash     = new DashboardViewModel(workoutRepo.Object, settingsRepo.Object);
        var log      = new LogWorkoutViewModel(workoutRepo.Object, settingsRepo.Object);
        var hist     = new HistoryViewModel(workoutRepo.Object, settingsRepo.Object);
        var sets     = new SettingsViewModel(settingsRepo.Object);
        var main     = new MainViewModel(dash, log, hist, sets);

        return (main, dash, log, hist, sets);
    }

    [Fact]
    public void DefaultCurrentViewModel_IsDashboard()
    {
        var (main, dash, _, _, _) = BuildAll();
        Assert.Same(dash, main.CurrentViewModel);
    }

    [Fact]
    public void NavigateToDashboardCommand_SetsDashboard()
    {
        var (main, dash, _, _, _) = BuildAll();
        main.NavigateToLogWorkoutCommand.Execute(null);

        main.NavigateToDashboardCommand.Execute(null);

        Assert.Same(dash, main.CurrentViewModel);
    }

    [Fact]
    public void NavigateToLogWorkoutCommand_SetsLogWorkout()
    {
        var (main, _, log, _, _) = BuildAll();
        main.NavigateToLogWorkoutCommand.Execute(null);
        Assert.Same(log, main.CurrentViewModel);
    }

    [Fact]
    public void NavigateToHistoryCommand_SetsHistory()
    {
        var (main, _, _, hist, _) = BuildAll();
        main.NavigateToHistoryCommand.Execute(null);
        Assert.Same(hist, main.CurrentViewModel);
    }

    [Fact]
    public void NavigateToSettingsCommand_SetsSettings()
    {
        var (main, _, _, _, sets) = BuildAll();
        main.NavigateToSettingsCommand.Execute(null);
        Assert.Same(sets, main.CurrentViewModel);
    }

    [Fact]
    public void NavigateCommands_FirePropertyChanged()
    {
        var (main, _, _, _, _) = BuildAll();
        string? changedProp = null;
        main.PropertyChanged += (_, e) => changedProp = e.PropertyName;

        main.NavigateToHistoryCommand.Execute(null);

        Assert.Equal(nameof(main.CurrentViewModel), changedProp);
    }

    [Fact]
    public void AllNavigateCommands_AlwaysCanExecute()
    {
        var (main, _, _, _, _) = BuildAll();
        Assert.True(main.NavigateToDashboardCommand.CanExecute(null));
        Assert.True(main.NavigateToLogWorkoutCommand.CanExecute(null));
        Assert.True(main.NavigateToHistoryCommand.CanExecute(null));
        Assert.True(main.NavigateToSettingsCommand.CanExecute(null));
    }

    [Fact]
    public void Constructor_NullDashboard_Throws()
    {
        var workoutRepo  = new Mock<IWorkoutRepository>();
        var settingsRepo = new Mock<IUserSettingsRepository>();
        settingsRepo.Setup(r => r.GetAsync()).ReturnsAsync(new UserSettings());
        var log  = new LogWorkoutViewModel(workoutRepo.Object, settingsRepo.Object);
        var hist = new HistoryViewModel(workoutRepo.Object, settingsRepo.Object);
        var sets = new SettingsViewModel(settingsRepo.Object);

        Assert.Throws<ArgumentNullException>(() =>
            new MainViewModel(null!, log, hist, sets));
    }
}
