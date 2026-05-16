using System.Windows.Input;
using TreadLog.ViewModels.Base;

namespace TreadLog.ViewModels;

/// <summary>
/// Shell ViewModel. Owns the side-navigation state and switches the active child ViewModel
/// in response to navigation commands. The View binds a ContentControl to CurrentViewModel
/// and uses DataTemplates to render the appropriate View for each ViewModel type.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private ViewModelBase _currentViewModel;

    public ViewModelBase CurrentViewModel
    {
        get => _currentViewModel;
        private set => SetProperty(ref _currentViewModel, value);
    }

    // Child ViewModels injected from the composition root (App.xaml.cs / DI container)
    private readonly DashboardViewModel  _dashboardVm;
    private readonly LogWorkoutViewModel _logWorkoutVm;
    private readonly HistoryViewModel    _historyVm;
    private readonly SettingsViewModel   _settingsVm;

    public ICommand NavigateToDashboardCommand  { get; }
    public ICommand NavigateToLogWorkoutCommand { get; }
    public ICommand NavigateToHistoryCommand    { get; }
    public ICommand NavigateToSettingsCommand   { get; }

    public MainViewModel(
        DashboardViewModel  dashboardVm,
        LogWorkoutViewModel logWorkoutVm,
        HistoryViewModel    historyVm,
        SettingsViewModel   settingsVm)
    {
        _dashboardVm  = dashboardVm  ?? throw new ArgumentNullException(nameof(dashboardVm));
        _logWorkoutVm = logWorkoutVm ?? throw new ArgumentNullException(nameof(logWorkoutVm));
        _historyVm    = historyVm    ?? throw new ArgumentNullException(nameof(historyVm));
        _settingsVm   = settingsVm   ?? throw new ArgumentNullException(nameof(settingsVm));

        NavigateToDashboardCommand  = new RelayCommand(() => NavigateTo(_dashboardVm));
        NavigateToLogWorkoutCommand = new RelayCommand(() => NavigateTo(_logWorkoutVm));
        NavigateToHistoryCommand    = new RelayCommand(() => NavigateTo(_historyVm));
        NavigateToSettingsCommand   = new RelayCommand(() => NavigateTo(_settingsVm));

        // Default landing screen
        _currentViewModel = _dashboardVm;
    }

    private void NavigateTo(ViewModelBase vm) => CurrentViewModel = vm;
}
