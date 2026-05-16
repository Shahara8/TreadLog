using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;
using TreadLog.Services;
using TreadLog.Services.Interfaces;
using TreadLog.ViewModels;
using TreadLog.Views;

namespace TreadLog;

public partial class App : Application
{
    private ServiceProvider? _provider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ── Database path (per-user, local app data) ──────────────────────────
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string dbDir   = Path.Combine(appData, "TreadLog");
        Directory.CreateDirectory(dbDir);
        string dbPath  = Path.Combine(dbDir, "treadlog.db");

        // ── Composition root ──────────────────────────────────────────────────
        var services = new ServiceCollection();

        // Infrastructure
        services.AddSingleton<IDatabaseService>(_ => new DatabaseService(dbPath));
        services.AddSingleton<IWorkoutRepository, WorkoutRepository>();
        services.AddSingleton<IUserSettingsRepository, UserSettingsRepository>();
        services.AddSingleton<IDataPortabilityService, DataPortabilityService>();

        // ViewModels (singletons — one instance per session, nav just reveals/hides)
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<LogWorkoutViewModel>();
        services.AddSingleton<SettingsViewModel>();

        // HistoryViewModel needs IDataPortabilityService
        services.AddSingleton<HistoryViewModel>(sp => new HistoryViewModel(
            sp.GetRequiredService<IWorkoutRepository>(),
            sp.GetRequiredService<IUserSettingsRepository>(),
            sp.GetRequiredService<IDataPortabilityService>()));

        services.AddSingleton<MainViewModel>();

        _provider = services.BuildServiceProvider();

        // ── Database initialization (sync on startup; tiny local SQLite) ──────
        var db = _provider.GetRequiredService<IDatabaseService>();
        db.InitializeAsync().GetAwaiter().GetResult();

        // ── Cross-ViewModel event wiring ──────────────────────────────────────
        var mainVm  = _provider.GetRequiredService<MainViewModel>();
        var logVm   = _provider.GetRequiredService<LogWorkoutViewModel>();
        var histVm  = _provider.GetRequiredService<HistoryViewModel>();
        var dashVm  = _provider.GetRequiredService<DashboardViewModel>();
        var setsVm  = _provider.GetRequiredService<SettingsViewModel>();

        // After editing a session from History → navigate back to History and refresh
        histVm.EditRequested += (_, session) =>
        {
            logVm.LoadSession(session);
            mainVm.NavigateToLogWorkoutCommand.Execute(null);
        };

        // After save → navigate to Dashboard and reload its data
        logVm.SaveSucceeded += (_, _) =>
        {
            mainVm.NavigateToDashboardCommand.Execute(null);
            dashVm.LoadDataCommand.Execute(null);
        };

        // When navigating to Settings, always reload the current unit preference
        mainVm.NavigateToSettingsCommand.Execute(null); // prime the VM
        mainVm.NavigateToDashboardCommand.Execute(null);

        // Bootstrap Dashboard data
        _ = dashVm.LoadDataAsync();
        _ = logVm.LoadSettingsAsync();
        _ = setsVm.LoadAsync();

        // ── Show shell ────────────────────────────────────────────────────────
        var window = new MainWindow { DataContext = mainVm };
        Current.MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _provider?.Dispose();
        base.OnExit(e);
    }
}
