using TreadLog.ViewModels.Base;
using Xunit;

namespace TreadLog.Tests.ViewModels;

public class RelayCommandTests
{
    // ── Parameterless constructor ─────────────────────────────────────────────

    [Fact]
    public void Parameterless_Execute_RunsAction()
    {
        bool ran = false;
        var cmd = new RelayCommand(() => ran = true);
        cmd.Execute(null);
        Assert.True(ran);
    }

    [Fact]
    public void Parameterless_CanExecute_DefaultsToTrue()
    {
        var cmd = new RelayCommand(() => { });
        Assert.True(cmd.CanExecute(null));
    }

    [Fact]
    public void Parameterless_CanExecute_ReturnsFalseWhenPredicate()
    {
        var cmd = new RelayCommand(() => { }, () => false);
        Assert.False(cmd.CanExecute(null));
    }

    [Fact]
    public void Parameterless_CanExecute_ReturnsTrueWhenPredicate()
    {
        var cmd = new RelayCommand(() => { }, () => true);
        Assert.True(cmd.CanExecute(null));
    }

    // ── Parameterised constructor ─────────────────────────────────────────────

    [Fact]
    public void Parameterised_Execute_ReceivesParameter()
    {
        object? received = null;
        var cmd = new RelayCommand(p => received = p);
        cmd.Execute("hello");
        Assert.Equal("hello", received);
    }

    [Fact]
    public void Parameterised_CanExecute_DefaultsToTrue()
    {
        var cmd = new RelayCommand(_ => { });
        Assert.True(cmd.CanExecute("anything"));
    }

    [Fact]
    public void Parameterised_CanExecute_UsesPredicateWithParameter()
    {
        var cmd = new RelayCommand(_ => { }, p => p is string s && s == "valid");
        Assert.True(cmd.CanExecute("valid"));
        Assert.False(cmd.CanExecute("invalid"));
    }

    // ── RaiseCanExecuteChanged ────────────────────────────────────────────────

    [Fact]
    public void RaiseCanExecuteChanged_FiresEvent()
    {
        var cmd = new RelayCommand(() => { });
        bool eventFired = false;
        cmd.CanExecuteChanged += (_, _) => eventFired = true;

        cmd.RaiseCanExecuteChanged();

        Assert.True(eventFired);
    }

    [Fact]
    public void RaiseCanExecuteChanged_WithNoSubscribers_DoesNotThrow()
    {
        var cmd = new RelayCommand(() => { });
        var ex = Record.Exception(() => cmd.RaiseCanExecuteChanged());
        Assert.Null(ex);
    }

    // ── Null guard ────────────────────────────────────────────────────────────

    [Fact]
    public void Parameterless_NullExecute_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RelayCommand((Action)null!));
    }

    [Fact]
    public void Parameterised_NullExecute_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RelayCommand((Action<object?>)null!));
    }
}
