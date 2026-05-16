using TreadLog.Models;
using TreadLog.Services;

namespace TreadLog.Tests.Services;

/// <summary>
/// Each test gets a fresh isolated SQLite file via IAsyncLifetime.
/// xUnit creates a new class instance per test, so _dbPath is unique per test.
/// </summary>
public class WorkoutRepositoryTests : IAsyncLifetime
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"tl_repo_{Guid.NewGuid():N}.db");

    private DatabaseService    _dbService = null!;
    private WorkoutRepository  _sut       = null!;

    public async Task InitializeAsync()
    {
        _dbService = new DatabaseService(_dbPath);
        await _dbService.InitializeAsync();
        _sut = new WorkoutRepository(_dbService);
    }

    public async Task DisposeAsync()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        await Task.Delay(20);
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* temp file — OS cleanup */ }
    }

    // ── Factory helper ─────────────────────────────────────────────────────────

    private static WorkoutSession Session(int dayOffset = 0) => new()
    {
        SessionDate     = new DateTime(2026, 5, 16, 7, 0, 0, DateTimeKind.Utc).AddDays(dayOffset),
        DurationSeconds = 3600,
        DistanceKm      = 10.0,
        AvgSpeedKmh     = 10.0,
        InclinePercent  = 2.0,
        CaloriesBurned  = 400,
        AvgHeartRate    = 140
    };

    // ── AddAsync ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_ReturnsPositiveId()
    {
        var id = await _sut.AddAsync(Session());
        Assert.True(id > 0);
    }

    [Fact]
    public async Task AddAsync_SecondInsert_ReturnsIncrementedId()
    {
        var id1 = await _sut.AddAsync(Session(0));
        var id2 = await _sut.AddAsync(Session(1));
        Assert.True(id2 > id1);
    }

    [Fact]
    public async Task AddAsync_WithNullOptionals_Succeeds()
    {
        var session = Session();
        session.CaloriesBurned = null;
        session.AvgHeartRate   = null;

        var id = await _sut.AddAsync(session);
        Assert.True(id > 0);
    }

    [Fact]
    public async Task AddAsync_NullOptionalsRoundTrip_ReturnNullFromDb()
    {
        var session = Session();
        session.CaloriesBurned = null;
        session.AvgHeartRate   = null;

        var id       = await _sut.AddAsync(session);
        var retrieved = await _sut.GetByIdAsync(id);

        Assert.NotNull(retrieved);
        Assert.Null(retrieved.CaloriesBurned);
        Assert.Null(retrieved.AvgHeartRate);
    }

    // ── GetByIdAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsMatchingFields()
    {
        var original = Session();
        var id       = await _sut.AddAsync(original);
        var result   = await _sut.GetByIdAsync(id);

        Assert.NotNull(result);
        Assert.Equal(id,                       result.Id);
        Assert.Equal(original.DurationSeconds, result.DurationSeconds);
        Assert.Equal(original.DistanceKm,      result.DistanceKm,     precision: 5);
        Assert.Equal(original.AvgSpeedKmh,     result.AvgSpeedKmh,    precision: 5);
        Assert.Equal(original.InclinePercent,  result.InclinePercent, precision: 5);
        Assert.Equal(original.CaloriesBurned,  result.CaloriesBurned);
        Assert.Equal(original.AvgHeartRate,    result.AvgHeartRate);
    }

    [Fact]
    public async Task GetByIdAsync_SessionDate_RoundTripsAsUtc()
    {
        var utcDate = new DateTime(2026, 5, 16, 7, 0, 0, DateTimeKind.Utc);
        var id      = await _sut.AddAsync(Session());
        var result  = await _sut.GetByIdAsync(id);

        Assert.NotNull(result);
        Assert.Equal(DateTimeKind.Utc, result.SessionDate.Kind);
        Assert.Equal(utcDate,          result.SessionDate);
    }

    [Fact]
    public async Task GetByIdAsync_AuditStrings_AreNonEmptyAfterInsert()
    {
        var id     = await _sut.AddAsync(Session());
        var result = await _sut.GetByIdAsync(id);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.CreatedAt));
        Assert.False(string.IsNullOrEmpty(result.UpdatedAt));
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        var result = await _sut.GetByIdAsync(99999);
        Assert.Null(result);
    }

    // ── GetAllAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_EmptyTable_ReturnsEmptyCollection()
    {
        var results = await _sut.GetAllAsync();
        Assert.Empty(results);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllInsertedSessions()
    {
        await _sut.AddAsync(Session(0));
        await _sut.AddAsync(Session(1));
        await _sut.AddAsync(Session(2));

        Assert.Equal(3, (await _sut.GetAllAsync()).Count());
    }

    [Fact]
    public async Task GetAllAsync_ResultsOrderedBySessionDateDescending()
    {
        await _sut.AddAsync(Session(0));
        await _sut.AddAsync(Session(2));
        await _sut.AddAsync(Session(1));

        var list = (await _sut.GetAllAsync()).ToList();

        Assert.True(list[0].SessionDate >= list[1].SessionDate);
        Assert.True(list[1].SessionDate >= list[2].SessionDate);
    }

    // ── GetByDateRangeAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetByDateRangeAsync_ReturnsOnlySessionsWithinRange()
    {
        await _sut.AddAsync(Session(-10)); // out of range: 2026-05-06
        await _sut.AddAsync(Session(0));   // in range:     2026-05-16
        await _sut.AddAsync(Session(1));   // in range:     2026-05-17

        var start   = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc).ToString("o");
        var end     = new DateTime(2026, 5, 18, 0, 0, 0, DateTimeKind.Utc).ToString("o");
        var results = await _sut.GetByDateRangeAsync(start, end);

        Assert.Equal(2, results.Count());
    }

    [Fact]
    public async Task GetByDateRangeAsync_NoMatches_ReturnsEmpty()
    {
        await _sut.AddAsync(Session(0)); // 2026-05-16

        var start   = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToString("o");
        var end     = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc).ToString("o");
        var results = await _sut.GetByDateRangeAsync(start, end);

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetByDateRangeAsync_ResultsOrderedBySessionDateAscending()
    {
        await _sut.AddAsync(Session(2));
        await _sut.AddAsync(Session(0));
        await _sut.AddAsync(Session(1));

        var start = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc).ToString("o");
        var end   = new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc).ToString("o");
        var list  = (await _sut.GetByDateRangeAsync(start, end)).ToList();

        Assert.True(list[0].SessionDate <= list[1].SessionDate);
        Assert.True(list[1].SessionDate <= list[2].SessionDate);
    }

    // ── UpdateAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ChangedFieldsPersistOnNextRead()
    {
        var id      = await _sut.AddAsync(Session());
        var session = await _sut.GetByIdAsync(id);
        session!.DistanceKm      = 15.75;
        session.DurationSeconds  = 5400;
        session.AvgSpeedKmh      = 10.5;
        session.CaloriesBurned   = 600;

        await _sut.UpdateAsync(session);
        var updated = await _sut.GetByIdAsync(id);

        Assert.NotNull(updated);
        Assert.Equal(15.75, updated.DistanceKm,    precision: 5);
        Assert.Equal(5400,  updated.DurationSeconds);
        Assert.Equal(10.5,  updated.AvgSpeedKmh,   precision: 5);
        Assert.Equal(600,   updated.CaloriesBurned);
    }

    [Fact]
    public async Task UpdateAsync_UpdatedAt_IsStampedWithCurrentTime()
    {
        var before  = DateTime.UtcNow.AddSeconds(-1);
        var id      = await _sut.AddAsync(Session());
        var session = (await _sut.GetByIdAsync(id))!;
        await _sut.UpdateAsync(session);

        var updated    = (await _sut.GetByIdAsync(id))!;
        var updatedAt  = DateTime.Parse(updated.UpdatedAt,
                             null, System.Globalization.DateTimeStyles.RoundtripKind);

        Assert.True(updatedAt >= before);
    }

    // ── DeleteAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_ExistingId_RemovesRow()
    {
        var id = await _sut.AddAsync(Session());
        await _sut.DeleteAsync(id);
        Assert.Null(await _sut.GetByIdAsync(id));
    }

    [Fact]
    public async Task DeleteAsync_NonExistentId_DoesNotThrow()
    {
        var ex = await Record.ExceptionAsync(() => _sut.DeleteAsync(99999));
        Assert.Null(ex);
    }

    [Fact]
    public async Task DeleteAsync_OnlyRemovesTargetRow()
    {
        var id1 = await _sut.AddAsync(Session(0));
        var id2 = await _sut.AddAsync(Session(1));

        await _sut.DeleteAsync(id1);

        Assert.Null(await _sut.GetByIdAsync(id1));
        Assert.NotNull(await _sut.GetByIdAsync(id2));
    }

    // ── BulkInsertIgnoreAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task BulkInsertIgnoreAsync_AllNew_ReturnsInsertedCount()
    {
        var sessions = new[] { Session(0), Session(1), Session(2) };
        var count    = await _sut.BulkInsertIgnoreAsync(sessions);
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task BulkInsertIgnoreAsync_AllDuplicates_ReturnsZero()
    {
        await _sut.AddAsync(Session(0));
        var count = await _sut.BulkInsertIgnoreAsync(new[] { Session(0) });
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task BulkInsertIgnoreAsync_MixedBatch_InsertsOnlyNew()
    {
        await _sut.AddAsync(Session(0)); // pre-existing

        var count = await _sut.BulkInsertIgnoreAsync(new[]
        {
            Session(0), // duplicate — skipped
            Session(1)  // new — inserted
        });

        Assert.Equal(1, count);
        Assert.Equal(2, (await _sut.GetAllAsync()).Count());
    }

    [Fact]
    public async Task BulkInsertIgnoreAsync_EmptyInput_ReturnsZero()
    {
        var count = await _sut.BulkInsertIgnoreAsync(Array.Empty<WorkoutSession>());
        Assert.Equal(0, count);
    }
}
