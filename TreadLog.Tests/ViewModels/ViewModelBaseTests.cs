using System.ComponentModel;
using TreadLog.ViewModels.Base;
using Xunit;

namespace TreadLog.Tests.ViewModels;

/// <summary>
/// Concrete subclass used only by the tests below.
/// All properties are proxies for the protected helpers in ViewModelBase.
/// </summary>
file sealed class TestViewModel : ViewModelBase
{
    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private int _count;
    public int Count
    {
        get => _count;
        set => SetProperty(ref _count, value);
    }

    // Exposes the return value of SetProperty for assertion
    public bool CallSetProperty<T>(ref T field, T value, string propertyName)
        => SetProperty(ref field, value, propertyName);

    public void RaiseManual(string name) => OnPropertyChanged(name);
}

public class ViewModelBaseTests
{
    [Fact]
    public void SetProperty_WhenValueChanges_ReturnsTrue()
    {
        var vm = new TestViewModel();
        bool result = false;
        string field = "old";
        result = vm.CallSetProperty(ref field, "new", nameof(field));
        Assert.True(result);
    }

    [Fact]
    public void SetProperty_WhenValueUnchanged_ReturnsFalse()
    {
        var vm = new TestViewModel();
        string field = "same";
        bool result = vm.CallSetProperty(ref field, "same", nameof(field));
        Assert.False(result);
    }

    [Fact]
    public void SetProperty_WhenValueChanges_FiresPropertyChanged()
    {
        var vm = new TestViewModel();
        var fired = new List<string>();
        vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName ?? "");

        vm.Name = "Alice";

        Assert.Contains(nameof(vm.Name), fired);
    }

    [Fact]
    public void SetProperty_WhenValueUnchanged_DoesNotFirePropertyChanged()
    {
        var vm = new TestViewModel { Name = "Alice" };
        var fired = new List<string>();
        vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName ?? "");

        vm.Name = "Alice"; // same value

        Assert.Empty(fired);
    }

    [Fact]
    public void SetProperty_UpdatesBackingField()
    {
        var vm = new TestViewModel();
        vm.Name = "Bob";
        Assert.Equal("Bob", vm.Name);
    }

    [Fact]
    public void OnPropertyChanged_FiresEventWithCorrectName()
    {
        var vm = new TestViewModel();
        string? captured = null;
        vm.PropertyChanged += (_, e) => captured = e.PropertyName;

        vm.RaiseManual("SomeProperty");

        Assert.Equal("SomeProperty", captured);
    }

    [Fact]
    public void MultipleSubscribers_AllReceivePropertyChanged()
    {
        var vm = new TestViewModel();
        int callCount = 0;
        vm.PropertyChanged += (_, _) => callCount++;
        vm.PropertyChanged += (_, _) => callCount++;

        vm.Count = 42;

        Assert.Equal(2, callCount);
    }
}
