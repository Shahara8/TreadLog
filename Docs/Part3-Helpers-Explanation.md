# Part 3 — Helpers Layer: Explanation for Review

## What Was Built

```
TreadLog/
└── Helpers/
    ├── UnitConverter.cs
    └── WorkoutValidator.cs

TreadLog.Tests/
└── Helpers/
    ├── UnitConverterTests.cs     (33 tests)
    └── WorkoutValidatorTests.cs  (51 tests)
```

Both helpers are **`static` classes** — stateless pure-function libraries with zero dependencies on services, models, or the database. They are the only place in the codebase where unit conversion and physical validation logic live. ViewModels call these helpers; they do not inline the math.

---

## Design Decisions

### 1. `UnitConverter` — Single Constant, Two Directions

```
private const double KmPerMile  = 1.60934;      // NIST statute mile
private const double MilesPerKm = 1.0 / KmPerMile;
```

All four conversion methods (`KmToMiles`, `MilesToKm`, `KmhToMph`, `MphToKmh`) derive from these two constants. If the constant ever needs updating, there is exactly one place to change it. The speed conversions reuse the same ratio — km/h × (miles/km) = mph — because the hour unit cancels.

### 2. `UnitConverter.CalculatePace` — Defence-in-Depth

The pace calculator applies **three independent guards** before dividing:

| Guard | What it catches |
|---|---|
| `IsFinitePositive(distanceKm)` | zero, negative, NaN, ±Infinity on the raw km input |
| `IsFinitePositive(durationSeconds)` | same for the duration |
| `IsFinitePositive(effectiveDistance)` | catches the post-conversion case where an astronomically small km value rounds to 0 when converted to miles |

None of these paths throw. They all return `TimeSpan.Zero`, which `FormatPace` formats as `"--:--"`. The UI binding sees a non-empty string in every case.

### 3. `FormatPace` — No Zero-Padding on Minutes

```
$"{totalMinutes}:{seconds:D2}"
```

Standard running-app convention: `"6:00"` not `"06:00"`. Seconds are always two digits (`D2`). `pace.Seconds` gives the 0–59 component seconds, **not** total seconds — so a 100-minute pace formats as `"100:00"`, not `"5:60"`.

Negative and zero `TimeSpan` values return `"--:--"`, providing a safe sentinel the ViewModel can bind to directly.

### 4. `WorkoutValidator.IsPhysicallyConsistent` — Relative Tolerance

```
relativeError = |actualDistance − expectedDistance| / expectedDistance
return relativeError ≤ 0.02  (2%)
```

**Why relative, not absolute?**  
An absolute tolerance of ±0.02 km would be far too permissive for a 0.1 km sprint and far too strict for a 50 km endurance run. A 2 % relative tolerance scales with workout magnitude: a 10 km run can deviate ±0.2 km (display rounding of speed/distance), while a 50 km run can deviate ±1 km — both of which are normal data-entry situations.

**Why 2%?**  
Speed is displayed to 1 decimal place and distance to 2 decimal places. The maximum rounding error for a 10 km/h speed is ±0.05 km/h → over 1 hour that drifts distance by 0.05 km = 0.5%. Two decimal places of distance rounding adds another 0.005 km = 0.05%. Combined worst-case is well below 2%, so any combination within tolerance is explainable by display rounding. Anything above 2% is a genuine logical error.

**Verification against the PRD example:**  
`IsPhysicallyConsistent(10.0, 5.0, 600)` → expected = 5 × (600/3600) = 0.833 km; relative error = |10 − 0.833| / 0.833 ≈ 11.0 (1100%) → `false` ✓

### 5. Auto-Calculation — All Three Directions Covered

| Method | Given | Returns |
|---|---|---|
| `CalculateDistance` | speed (km/h) + duration (s) | distance (km) |
| `CalculateDuration` | distance (km) + speed (km/h) | duration (seconds, rounded) |
| `CalculateSpeed`    | distance (km) + duration (s) | speed (km/h) |

`CalculateDuration` rounds to the nearest whole second via `Math.Round` — the ViewModel binds `DurationSeconds` as `int`, so fractional seconds are never exposed. All three methods return `0` for any non-positive input; no exceptions escape to the ViewModel.

The tests include **round-trip consistency checks**: compute a metric, then verify `IsPhysicallyConsistent` on the result. This proves the validator and the calculator agree — they are not independent approximations.

---

## Test Coverage Summary

| Class | Tests | Scenarios |
|---|---|---|
| `UnitConverterTests` | 33 | Known-value conversions (km↔mi, km/h↔mph), round-trips, zero/negative, NaN, ±Infinity, very-small/very-large, pace in both units, format edge cases (zero, negative, triple-digit minutes, single-digit seconds zero-pad) |
| `WorkoutValidatorTests` | 51 | Each field validator boundary (×6 fields), consistency: perfect match, within tolerance, exact boundary, just above boundary, PRD example, clearly impossible, all degenerate inputs; auto-calc: all three directions with normal + degenerate inputs, round-trip consistency proofs, all division-by-zero vectors |

**Total new tests this part: 84**  
**Cumulative test count: 139** (55 from Parts 1–2 + 84 from Part 3)
