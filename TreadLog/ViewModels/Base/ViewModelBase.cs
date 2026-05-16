using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TreadLog.ViewModels.Base;

/// <summary>
/// Root base class for every ViewModel.
/// Provides SetProperty (change-guard + notify) and OnPropertyChanged.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raises PropertyChanged for the calling member or the supplied name.</summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// Assigns <paramref name="value"/> to <paramref name="field"/> only when the value differs.
    /// Fires <see cref="PropertyChanged"/> and returns <c>true</c> when an assignment occurred;
    /// returns <c>false</c> and leaves the field unchanged when the value is equal.
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
