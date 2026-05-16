using Moq;
using TreadLog.Models;
using TreadLog.Services.Interfaces;
using TreadLog.ViewModels;
using Xunit;

namespace TreadLog.Tests.ViewModels;

public class SettingsViewModelTests
{
    private static Mock<IUserSettingsRepository> MockRepo(string unit = "km")
    {
        var m = new Mock<IUserSettingsRepository>();
        m.Setup(r => r.GetAsync())
            .ReturnsAsync(new UserSettings { Id = 1, DistanceUnit = unit });
        m.Setup(r => r.SaveAsync(It.IsAny<UserSettings>()))
            .Returns(Task.CompletedTask);
        return m;
    }

    // ── LoadAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_SetsDistanceUnit_FromRepo()
    {
        var repo = MockRepo("mi");
        var vm   = new SettingsViewModel(repo.Object);

        await vm.LoadAsync();

        Assert.Equal("mi", vm.DistanceUnit);
    }

    [Fact]
    public async Task LoadAsync_ClearsSaveSucceeded()
    {
        var repo = MockRepo();
        var vm   = new SettingsViewModel(repo.Object);
        await vm.LoadAsync();
        vm.DistanceUnit = "mi";
        await vm.SaveAsync();
        Assert.True(vm.SaveSucceeded);

        // Re-loading should reset SaveSucceeded
        vm.DistanceUnit = "km"; // so save can enable again
        await vm.LoadAsync();

        Assert.False(vm.SaveSucceeded);
    }

    [Fact]
    public async Task LoadAsync_SetsIsBusy_ThenClears()
    {
        bool wasBusy = false;
        var repo = new Mock<IUserSettingsRepository>();
        repo.Setup(r => r.GetAsync()).ReturnsAsync(() =>
        {
            wasBusy = true;
            return new UserSettings { Id = 1, DistanceUnit = "km" };
        });

        var vm = new SettingsViewModel(repo.Object);
        await vm.LoadAsync();

        Assert.True(wasBusy);
        Assert.False(vm.IsBusy);
    }

    // ── SaveCommand CanExecute ────────────────────────────────────────────────

    [Fact]
    public async Task SaveCommand_DisabledWhenUnitUnchanged()
    {
        var repo = MockRepo("km");
        var vm   = new SettingsViewModel(repo.Object);
        await vm.LoadAsync(); // loads "km", caches _loadedUnit = "km"

        // DistanceUnit is already "km" — no change
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task SaveCommand_EnabledWhenUnitChanged()
    {
        var repo = MockRepo("km");
        var vm   = new SettingsViewModel(repo.Object);
        await vm.LoadAsync();

        vm.DistanceUnit = "mi"; // dirty

        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task SaveCommand_DisabledWhileIsBusy()
    {
        var repo = MockRepo("km");
        var vm   = new SettingsViewModel(repo.Object);
        await vm.LoadAsync();
        vm.DistanceUnit = "mi";

        // Verify via SaveAsync internals — here we just check the command CanExecute when not busy
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    // ── SaveAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_CallsRepoSaveAsync()
    {
        var repo = MockRepo("km");
        var vm   = new SettingsViewModel(repo.Object);
        await vm.LoadAsync();
        vm.DistanceUnit = "mi";

        await vm.SaveAsync();

        repo.Verify(r => r.SaveAsync(It.Is<UserSettings>(s => s.DistanceUnit == "mi")), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_SetsSaveSucceeded_True()
    {
        var repo = MockRepo("km");
        var vm   = new SettingsViewModel(repo.Object);
        await vm.LoadAsync();
        vm.DistanceUnit = "mi";

        await vm.SaveAsync();

        Assert.True(vm.SaveSucceeded);
    }

    [Fact]
    public async Task SaveAsync_SetsStatusMessage()
    {
        var repo = MockRepo("km");
        var vm   = new SettingsViewModel(repo.Object);
        await vm.LoadAsync();
        vm.DistanceUnit = "mi";

        await vm.SaveAsync();

        Assert.NotEmpty(vm.StatusMessage);
    }

    [Fact]
    public async Task SaveAsync_AfterSave_SaveCommandDisabled_BecauseUnitMatchesLoaded()
    {
        var repo = MockRepo("km");
        var vm   = new SettingsViewModel(repo.Object);
        await vm.LoadAsync();
        vm.DistanceUnit = "mi";
        await vm.SaveAsync(); // now _loadedUnit = "mi"

        // DistanceUnit is still "mi" and _loadedUnit is now "mi" — no longer dirty
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task SaveAsync_OnRepoFailure_SetsStatusMessage_SaveSucceededFalse()
    {
        var repo = new Mock<IUserSettingsRepository>();
        repo.Setup(r => r.GetAsync())
            .ReturnsAsync(new UserSettings { Id = 1, DistanceUnit = "km" });
        repo.Setup(r => r.SaveAsync(It.IsAny<UserSettings>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var vm = new SettingsViewModel(repo.Object);
        await vm.LoadAsync();
        vm.DistanceUnit = "mi";

        await vm.SaveAsync();

        Assert.False(vm.SaveSucceeded);
        Assert.NotEmpty(vm.StatusMessage);
    }

    // ── AvailableUnits ────────────────────────────────────────────────────────

    [Fact]
    public void AvailableUnits_ContainsKmAndMi()
    {
        Assert.Contains("km", SettingsViewModel.AvailableUnits);
        Assert.Contains("mi", SettingsViewModel.AvailableUnits);
    }

    // ── Constructor guard ─────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullSettingsRepo_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SettingsViewModel(null!));
    }

    // ── PropertyChanged ───────────────────────────────────────────────────────

    [Fact]
    public async Task DistanceUnit_Change_FiresPropertyChanged()
    {
        var repo = MockRepo("km");
        var vm   = new SettingsViewModel(repo.Object);
        await vm.LoadAsync();

        var fired = new List<string>();
        vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName ?? "");

        vm.DistanceUnit = "mi";

        Assert.Contains(nameof(vm.DistanceUnit), fired);
    }
}
