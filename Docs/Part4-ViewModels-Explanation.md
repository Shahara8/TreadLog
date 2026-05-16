# Part 4 — ViewModel Layer: Explanation for Review

## What was built

Part 4 delivers the complete ViewModel layer: six ViewModels, two base infrastructure classes, two support types, and 70+ unit tests. No View or repository code was touched.

---

## File inventory

| File | Role |
|---|---|
| `TreadLog/ViewModels/Base/ViewModelBase.cs` | Root base class — INotifyPropertyChanged + SetProperty |
| `TreadLog/ViewModels/Base/RelayCommand.cs` | ICommand implementation with CanExecute + RaiseCanExecuteChanged |
| `TreadLog/ViewModels/Support/DateFilterOption.cs` | Enum: Last7Days / Last30Days / YearToDate / Custom |
| `TreadLog/ViewModels/Support/ChartDataPoint.cs` | `sealed record ChartDataPoint(string Label, double Value)` |
| `TreadLog/ViewModels/MainViewModel.cs` | Shell — owns navigation and the active child ViewModel |
| `TreadLog/ViewModels/DashboardViewModel.cs` | Landing screen — KPI aggregation, date filtering, chart data |
| `TreadLog/ViewModels/LogWorkoutViewModel.cs` | Form ViewModel — full validation, edit mode, auto-calculation |
| `TreadLog/ViewModels/HistoryViewModel.cs` | Session list — search, date filter, edit/delete delegation |
| `TreadLog/ViewModels/SettingsViewModel.cs` | Settings screen — unit preference load/save |
| `TreadLog.Tests/ViewModels/ViewModelBaseTests.cs` | 7 tests |
| `TreadLog.Tests/ViewModels/RelayCommandTests.cs` | 10 tests |
| `TreadLog.Tests/ViewModels/MainViewModelTests.cs` | 7 tests |
| `TreadLog.Tests/ViewModels/DashboardViewModelTests.cs` | 14 tests |
| `TreadLog.Tests/ViewModels/LogWorkoutViewModelTests.cs` | 26 tests |
| `TreadLog.Tests/ViewModels/HistoryViewModelTests.cs` | 14 tests |
| `TreadLog.Tests/ViewModels/SettingsViewModelTests.cs` | 13 tests |

---

## Key design decisions

### 1. ViewModelBase — change-guard pattern

`SetProperty<T>` uses `EqualityComparer<T>.Default` to compare the incoming value with the backing field before writing. This means:

- No spurious `PropertyChanged` events when the value is the same.
- Return value (`bool`) lets callers know whether an assignment actually occurred — used by `DashboardViewModel.SelectedFilter` to guard the auto-reload.

```csharp
protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
{
    if (EqualityComparer<T>.Default.Equals(field, value)) return false;
    field = value;
    OnPropertyChanged(propertyName);
    return true;
}
```

### 2. RelayCommand — two constructors, one implementation

`RelayCommand` accepts either a parameterised `Action<object?>` or a parameterless `Action`. The parameterless overload delegates to the parameterised one via `_ => execute()`, keeping a single code path for `Execute` and `CanExecute`. Callers raise `CanExecuteChanged` explicitly via `RaiseCanExecuteChanged()` — no `CommandManager.RequerySuggested` dependency, which keeps the class testable without a WPF dispatcher.

### 3. MainViewModel — composition root navigation

`MainViewModel` holds pre-built child ViewModels injected at construction time (by the DI container in Part 5). Navigation is pure assignment — `CurrentViewModel = vm`. The View binds a `ContentControl` to `CurrentViewModel` and uses DataTemplates keyed on ViewModel type to select the correct View. This is the standard MVVM shell pattern: no View references in the ViewModel.

### 4. DashboardViewModel — async fire-and-forget with filter guard

Date filter changes on non-Custom options trigger `LoadDataAsync` via `_ = LoadDataAsync()`. This is intentional: `ICommand.Execute` is `void`; returning a task would require an `async void` lambda, which swallows exceptions the same way. The `_ = ` discard makes the fire-and-forget intent explicit.

The `Custom` filter path is different: no auto-reload. Instead the `ApplyCustomFilterCommand` gate (`CanExecute`) requires both `CustomStartDate` and `CustomEndDate` to be set and `Start ≤ End`. This prevents a query with a partial date range.

### 5. DashboardViewModel — chart data is OxyPlot-agnostic

`WeeklyDistancePoints`, `SpeedTrendPoints`, and `InclineTrendPoints` are `ObservableCollection<ChartDataPoint>` — not OxyPlot types. Part 5 will wire these into actual chart series. This keeps the ViewModel fully testable without a rendering dependency.

Weekly grouping uses `StartOfWeek(date)`:

```csharp
private static DateTime StartOfWeek(DateTime date)
{
    int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
    return date.Date.AddDays(-diff);
}
```

Sessions in the same ISO week are aggregated into a single bar. Labels use `"MMM d"` (e.g. `"Jun 2"`).

### 6. LogWorkoutViewModel — metric-internal, display-external

All backing fields (`_distanceKm`, `_avgSpeedKmh`) store SI metric values. Conversion happens only at input parsing and display formatting. When the user switches units (`SelectedUnit`), `ConvertDisplayValuesOnUnitChange` re-formats the existing backing values into the new unit — the backing state does not change.

```
User types "1.0 mi"
  → ParseAndValidateDistance() converts 1.0 mi → 1.60934 km → stores _distanceKm
User switches to km
  → ConvertDisplayValuesOnUnitChange() re-displays _distanceKm (1.60934) as "1.61 km"
```

### 7. LogWorkoutViewModel — consistency warning is non-blocking

The physical consistency check (`WorkoutValidator.IsPhysicallyConsistent`) runs in `UpdateFormState` but sets only `ConsistencyWarning` — a warning string, not an error. `IsFormValid` is computed from the six error properties (distance, speed, duration, incline, calories, heart rate). A session with inconsistent values is still saveable; the warning exists to catch data entry mistakes, not to prevent valid sessions where the user knows the values are approximate.

### 8. LogWorkoutViewModel — edit mode via LoadSession

`LoadSession(WorkoutSession)` sets `_editingSessionId` (non-null → `IsEditing = true`) and populates all display fields from the incoming session, converting to the current display unit. `SaveAsync` checks `_editingSessionId.HasValue` to decide between `AddAsync` and `UpdateAsync`. `ClearForm` resets `_editingSessionId` to null, returning to create mode.

### 9. HistoryViewModel — WorkoutSessionRow projection

Rather than binding the View directly to `WorkoutSession`, the History list uses `WorkoutSessionRow` — a plain sealed class that pre-computes all display strings (date, distance with unit, duration as `H:MM:SS`, pace, speed, incline, calories, heart rate). This keeps all formatting logic in C# where it is testable, and keeps the View's DataTemplate free of converter objects.

### 10. SettingsViewModel — dirty-state CanExecute

`SaveCommand.CanExecute` returns `!IsBusy && DistanceUnit != _loadedUnit`. `_loadedUnit` is set on `LoadAsync` and updated on each successful `SaveAsync`. This means the Save button is naturally disabled when the setting matches what's already persisted, and re-enables as soon as the user picks a different option.

---

## Test strategy

All tests mock `IWorkoutRepository` and `IUserSettingsRepository` with Moq. No database is touched in Part 4.

| Pattern | Where used |
|---|---|
| Inline mock setup per test | Most tests — minimal shared state |
| `Build()` factory helper | Reduces boilerplate while keeping tests readable |
| `await vm.LoadDataAsync()` directly | Tests the public async method, not the ICommand wrapper |
| `vm.SaveCommand.CanExecute(null)` | Validates gate logic without executing the command |
| `vm.PropertyChanged +=` event capture | ViewModelBase and navigation tests |
| `repo.Verify(...)` | Confirms repo methods called with correct arguments |

---

## Contracts with Part 5

Part 5 (Views + wiring) can rely on these ViewModel guarantees:

- `LoadDataCommand` and `LoadCommand` are safe to bind to `Loaded`/`IsVisibleChanged` events.
- `SaveSucceeded` (LogWorkoutViewModel) and `EditRequested` (HistoryViewModel) are C# events that Part 5 subscribes to in order to trigger navigation.
- All `ObservableCollection<T>` properties (`Sessions`, `WeeklyDistancePoints`, etc.) are ready for direct `ItemsSource` / `Series` binding.
- `AvailableUnits` (SettingsViewModel) is a static `IReadOnlyList<string>` safe for `ItemsSource` on a ComboBox or RadioButtons.
