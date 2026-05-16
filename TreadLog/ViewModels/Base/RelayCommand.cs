using System.Windows.Input;

namespace TreadLog.ViewModels.Base;

/// <summary>
/// General-purpose ICommand implementation.
/// Two constructors: one accepting an optional object parameter, one parameterless — both
/// support an optional CanExecute predicate.
/// Call <see cref="RaiseCanExecuteChanged"/> after any state change that affects executability.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    /// <summary>Parameterised constructor. The execute/canExecute delegates receive the command parameter.</summary>
    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute    = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>Parameterless convenience constructor.</summary>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute is null ? null : _ => canExecute())
    {
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    /// <inheritdoc />
    public void Execute(object? parameter) => _execute(parameter);

    /// <summary>
    /// Signals the UI to re-query <see cref="CanExecute"/>.
    /// Call after any state change that might change executability (e.g. form validation).
    /// </summary>
    public void RaiseCanExecuteChanged()
        => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
