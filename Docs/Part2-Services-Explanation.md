# Part 2 — Services Layer: Explanation for Review

## What Was Built

### Model Corrections (carry-forward from Part 1 feedback)

| Field | Before | After | Reason |
|---|---|---|---|
| `WorkoutSession.CreatedAt` | `DateTime` | `string` | Mirrors SQLite TEXT verbatim; no parsing in the Model layer |
| `WorkoutSession.UpdatedAt` | `DateTime` | `string` | Same as above |
| `UserSettings.UpdatedAt`   | `DateTime` | `string` | Consistency — all audit timestamps are opaque strings in Models |

The Model tests were updated to assert the raw ISO 8601 string is returned unchanged, with a dedicated test (`AuditStrings_StoreRawIso8601WithoutParsing`) that intentionally passes a non-UTC offset string and verifies the Model does not modify it.

---

### New Files

```
TreadLog/
└── Services/
    ├── Interfaces/
    │   ├── IDatabaseService.cs
    │   ├── IWorkoutRepository.cs
    │   └── IUserSettingsRepository.cs
    ├── DatabaseService.cs
    ├── WorkoutRepository.cs
    └── UserSettingsRepository.cs

TreadLog.Tests/
└── Services/
    ├── DatabaseServiceTests.cs        (10 tests)
    ├── WorkoutRepositoryTests.cs      (22 tests)
    └── UserSettingsRepositoryTests.cs  (7 tests)
```

---

## Design Decisions

### 1. `DatabaseService` — Transactional Schema Init

All DDL (CREATE TABLE, CREATE INDEX, INSERT OR IGNORE seed) runs inside a **single transaction**. If any statement fails mid-schema, the transaction rolls back and leaves the database file in its previous state — there are no half-initialised schemas.

All DDL uses `IF NOT EXISTS` / `INSERT OR IGNORE`, making `InitializeAsync()` **idempotent** — safe to call on every application startup without checking version numbers.

### 2. `WorkoutRepository` — Parameterised Queries Everywhere

Every SQL statement uses named `@Parameter` placeholders via `cmd.Parameters.AddWithValue(...)`. No string interpolation or concatenation is used in query construction. This is the primary defence against SQL injection.

### 3. Date Normalisation Strategy

| Value | Stored As | Round-tripped As |
|---|---|---|
| `SessionDate` (business DateTime) | UTC ISO 8601 via `.ToUniversalTime().ToString("o")` | `DateTime` with `Kind = Utc`, via `RoundtripKind` parse flag |
| `CreatedAt`, `UpdatedAt` (audit strings) | Raw ISO 8601 string from the `@CreatedAt` / `@UpdatedAt` parameters | Verbatim `string` — no parsing in `MapRow` |

Normalising `SessionDate` to UTC before storage means SQLite's lexicographic TEXT comparisons in `GetByDateRangeAsync` are always correct — there is no timezone ambiguity in the stored values.

`UpdatedAt` is always stamped by the repository with `DateTime.UtcNow.ToString("o")` on every write — the caller never needs to set it.

### 4. `BulkInsertIgnoreAsync` — Transactional Import with Dedup

The import path wraps all inserts in a single transaction and uses `INSERT OR IGNORE`. If the batch fails mid-way, the entire transaction rolls back — no partial imports reach the database. The `UNIQUE INDEX` on `SessionDate` is the dedup enforcement point; the repository does not need to query first.

### 5. `UserSettingsRepository` — Single-Row Guard

The `CHECK (Id = 1)` constraint on the `UserSettings` table (defined in the schema) is the hard guarantee against duplicate rows. `SaveAsync` issues an `UPDATE WHERE Id = 1` — it cannot accidentally insert a second row.

### 6. Test Isolation

Each test class creates a uniquely named temp `.db` file via `Guid.NewGuid()`. Since xUnit constructs a **new class instance per test**, `IAsyncLifetime.InitializeAsync` runs `DatabaseService.InitializeAsync()` before each test and `DisposeAsync` deletes the file after — every test is fully isolated with zero shared state.

**No Moq is used in the services layer tests.** Mocking a database for repository tests hides real SQL bugs. Instead, tests hit a genuine in-memory-equivalent SQLite file. Moq is reserved for the ViewModel layer (Part 4), where the repositories are the dependency to mock.

---

## Test Coverage Summary

| Class | Tests | Scenarios Covered |
|---|---|---|
| `DatabaseServiceTests` | 10 | Table creation, index creation, seed row, idempotency (×2), row count, connection factory |
| `WorkoutRepositoryTests` | 22 | Add (3), GetById (4), GetAll (3), GetByDateRange (3), Update (2), Delete (3), BulkInsert (4) |
| `UserSettingsRepositoryTests` | 7 | Get after init, non-empty UpdatedAt, valid ISO 8601, unit change, toggle back, timestamp stamp, no-duplicate-row |

**Total new tests this part: 39**
**Cumulative test count: 55** (16 from Part 1 + 39 from Part 2)
