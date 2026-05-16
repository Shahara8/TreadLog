# Part 5 — Views, DataPortabilityService & DI Bootstrap

## Overview

Part 5 completes the alpha build by wiring the MVVM stack end-to-end: XAML views bind to ViewModels from Part 4, a DI container composes the object graph at startup, and a `DataPortabilityService` handles structured CSV/JSON export and import against the same dedup logic used by `WorkoutRepository`.

---

## DataPortabilityService

### Design goals

- **Round-trip fidelity**: every numeric field stored as a metric value (`DistanceKm`, `AvgSpeedKmh`) is exported as-is; no unit conversion at the persistence boundary.
- **Dedup safety**: import routes through `IWorkoutRepository.BulkInsertIgnoreAsync`, which executes `INSERT OR IGNORE` keyed on the `SessionDate` unique index — the same path used by the treadmill sync flow, so duplicate-date protection is not duplicated.
- **No Id in export payload**: exported `SessionExportDto` intentionally omits `Id` and audit timestamps (`CreatedAt`, `UpdatedAt`). Ids are meaningless across installs; audit columns are regenerated on insert.

### CSV format

```
Id,SessionDate,DurationSeconds,DistanceKm,AvgSpeedKmh,InclinePercent,CaloriesBurned,AvgHeartRate,CreatedAt,UpdatedAt
1,2026-05-16T07:00:00.0000000Z,3600,10.0,10.0,2.0,400,140,...,...
```

- UTF-8, no BOM.
- Header row is validated on import: every required column must be present (order-independent; a superset of columns is accepted).
- `SplitCsvLine` is a minimal RFC-4180 implementation that handles quoted fields with embedded commas and escaped double-quotes (`""`).

### JSON format

```json
[
  {
    "SessionDate": "2026-05-16T07:00:00.0000000Z",
    "DurationSeconds": 3600,
    "DistanceKm": 10.0,
    "AvgSpeedKmh": 10.0,
    "InclinePercent": 2.0,
    "CaloriesBurned": 400,
    "AvgHeartRate": 140
  }
]
```

- Serialized with `WriteIndented = true` and `DefaultIgnoreCondition = WhenWritingNull` so optional fields are omitted cleanly.
- Import dispatches by file extension (`.csv` / `.json`); anything else throws `NotSupportedException`.

### Error handling

`ImportFromFileAsync` returns `(int inserted, IReadOnlyList<string> errors)`. Individual row failures (bad date format, missing required field, out-of-range value) are collected into the errors list and reported to the UI without aborting the rest of the batch.

---

## App.xaml — Theme & Resources

### Color palette

| Key | Hex | Usage |
|-----|-----|-------|
| `AppBackground` | `#0F172A` | Window background |
| `SidebarBackground` | `#1E293B` | Nav sidebar |
| `CardBackground` | `#1E293B` | KPI cards, form panels |
| `AccentBrush` | `#7C3AED` | Primary buttons, active nav |
| `AccentHover` | `#6D28D9` | Button hover state |
| `TextPrimary` | `#F1F5F9` | Body text |
| `TextMuted` | `#94A3B8` | Labels, placeholders |
| `BorderBrush` | `#334155` | Input borders |
| `WarningBackground` | `#7C3300` | Consistency warning banner |

### Converters (declared as static resources)

| Key | Type | Purpose |
|-----|------|---------|
| `BoolToVisibilityConverter` | `IValueConverter` | `bool` → `Visibility` |
| `NotEmptyToVisibilityConverter` | `IValueConverter` | non-empty string → `Visible`, else `Collapsed` |
| `StringEqualConverter` | `IValueConverter` | string ↔ `bool`; `ConverterParameter` = expected value |
| `EnumToBoolConverter` | `IValueConverter` | Enum ↔ `bool` for RadioButton groups |
| `EnumToVisibilityConverter` | `IValueConverter` | Enum → `Visibility` for conditional panels |

`StringEqualConverter` is used in `LogWorkoutView` and `SettingsView` for unit RadioButtons (`ConverterParameter="km"` / `ConverterParameter="mi"`). It implements `ConvertBack` so two-way binding works: selecting a RadioButton writes the string value back to `DistanceUnit`.

### DataTemplate navigation

Shell navigation is implemented via `DataTemplate` entries keyed on ViewModel type — no navigation service or frame:

```xml
<DataTemplate DataType="{x:Type vm:DashboardViewModel}">
    <views:DashboardView/>
</DataTemplate>
```

`MainWindow` binds a `ContentControl.Content` to `MainViewModel.CurrentViewModel`. WPF selects the matching `DataTemplate` automatically. View instances are created lazily on first navigation and reused (ViewModels are singletons in the DI container, so state is preserved across navigation).

---

## App.xaml.cs — DI Bootstrap

### Composition root

```
IDatabaseService (singleton)
  └─ IWorkoutRepository (singleton)
  └─ IUserSettingsRepository (singleton)
  └─ IDataPortabilityService (singleton)
       ├─ DashboardViewModel (singleton)
       ├─ LogWorkoutViewModel (singleton)
       ├─ SettingsViewModel (singleton)
       ├─ HistoryViewModel (singleton) ← receives IDataPortabilityService
       └─ MainViewModel (singleton)
```

`Microsoft.Extensions.DependencyInjection` is used; no third-party DI container.

### Database path

```
%LOCALAPPDATA%\TreadLog\treadlog.db
```

The directory is created if it does not exist. `DatabaseService.InitializeAsync()` is called synchronously on the UI thread at startup (via `.GetAwaiter().GetResult()`) before the window is shown. This is acceptable because the operation is fast (schema creation is idempotent DDL) and avoids `async void OnStartup`.

### Cross-ViewModel event wiring

Two events are wired in `OnStartup` after the DI container is built:

| Event | Source | Action |
|-------|--------|--------|
| `HistoryViewModel.EditRequested` | HistoryView's Edit button | Loads the session into `LogWorkoutViewModel`, navigates to Log screen |
| `LogWorkoutViewModel.SaveSucceeded` | Log form save | Navigates to Dashboard, triggers `DashboardViewModel.LoadDataCommand` |

These are one-way event subscriptions; the ViewModels themselves have no reference to each other, preserving the MVVM separation.

---

## Views

### MainWindow.xaml

Two-column `Grid`: 220 px sidebar + `*` content area.

- **Sidebar**: app title, four nav `Button` controls bound to `MainViewModel.Navigate*Command`, version label docked to the bottom.
- **Content**: single `ContentControl` bound to `MainViewModel.CurrentViewModel`; DataTemplates in `App.xaml` handle the rest.

The `NavButtonStyle` draws a left accent stripe (`Border` with `Width=4`) when the button is active (via `DataTrigger` comparing `MainViewModel.CurrentViewModel` type to a static resource tag on each button).

### DashboardView.xaml / .xaml.cs

- **Date filter**: `RadioButton` group using `EnumToBoolConverter` (ConverterParameter = enum member name string). Custom date pickers are shown/hidden via `EnumToVisibilityConverter`.
- **KPI cards**: four `Border` panels in a `UniformGrid`, each binding to a pre-formatted display string on `DashboardViewModel` (`TotalDistanceDisplay`, `TotalTimeDisplay`, `AveragePaceDisplay`, `SessionCount`).
- **Charts**: two `oxy:PlotView` controls with `x:Name`; the models are built entirely in code-behind (`DashboardView.xaml.cs`) to keep OxyPlot APIs out of the ViewModel.
  - `WeeklyDistanceChart`: `BarSeries` + `CategoryAxis` (day labels) + `LinearAxis`.
  - `SpeedInclineTrendChart`: two `LineSeries` (avg speed, avg incline) sharing a `DateTimeAxis`.
  - Both charts subscribe to `ObservableCollection<ChartDataPoint>.CollectionChanged` to rebuild when the ViewModel pushes new data.
  - OxyPlot 2.1.x legend API: `model.Legends.Add(new OxyPlot.Legends.Legend { ... })` — legend properties were moved out of `PlotModel` in this version.

### LogWorkoutView.xaml

- Unit RadioButtons (`km` / `mi`) use `StringEqualConverter` for two-way binding to `DistanceUnit`.
- "Calc" buttons next to Distance and Speed trigger `AutoCalcDistanceCommand` / `AutoCalcSpeedCommand` — single-field auto-fill based on the other two of the distance/speed/duration triangle.
- `ConsistencyWarning` is an orange `Border` visible when `ConsistencyWarningMessage` is non-empty (`NotEmptyToVisibilityConverter`).
- All numeric TextBoxes are `UpdateSourceTrigger=PropertyChanged` so validation fires on each keystroke.

### HistoryView.xaml / .xaml.cs

- Filter bar: free-text search (`SearchText`), date range pickers, Apply/Clear buttons.
- Export/Import: `Button.Click` handlers in code-behind open `SaveFileDialog` / `OpenFileDialog` (dialog is a View concern) and pass the selected path to `vm.ExportCsvCommand.Execute(path)` / `vm.ImportCommand.Execute(path)`.
- `ImportStatusMessage` TextBlock is visible when non-empty.
- `DataGrid` columns bind to `WorkoutSessionRow` pre-formatted display properties; no `IValueConverter` needed in XAML.
- Edit and Delete action buttons in the last column invoke `vm.EditCommand` and `vm.DeleteCommand` with the row as the command parameter.

### SettingsView.xaml / .xaml.cs

- Two RadioButtons bound to `DistanceUnit` via `StringEqualConverter`.
- `SaveCommand.CanExecute` returns true only when `DistanceUnit != _loadedUnit && !IsBusy`, so the Save button is disabled until a change is made.
- `OnLoaded` in code-behind executes `LoadCommand` to hydrate from the database.
- A `TextBlock` bound to `StatusMessage` with `BoolToVisibilityConverter` on `SaveSucceeded` shows a brief confirmation after save.

---

## Test coverage

| Area | Tests added in Part 5 |
|------|-----------------------|
| `DataPortabilityService` | 20 (CSV export, JSON export, CSV import, JSON import, error paths) |
| `RelayCommand` null guard fix | 1 (existing test now passes) |

**Total solution tests: 329 — all green.**

---

## Build notes

- `TreadLog/GlobalUsings.cs` adds `global using System.IO;` to work around a WPF SDK implicit-using gap that affected `DataPortabilityService` and `App.xaml.cs`.
- `TreadLog.Tests/GlobalUsings.cs` adds `global using System.IO; global using Xunit; global using Moq;` to cover all test files without per-file `using` directives.
- OxyPlot `PlotAreaBorderColor` is a `PlotModel` property, not a XAML attached property on `PlotView`; it is set in C# code-behind only.
