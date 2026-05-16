using Microsoft.Data.Sqlite;
using TreadLog.Services;

namespace TreadLog.Tests.Services;

public class DatabaseServiceTests : IAsyncLifetime
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"tl_db_{Guid.NewGuid():N}.db");

    private DatabaseService _sut = null!;

    public async Task InitializeAsync()
    {
        _sut = new DatabaseService(_dbPath);
        await _sut.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
        return Task.CompletedTask;
    }

    // ── Schema creation ────────────────────────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_CreatesWorkoutSessionsTable()
    {
        var name = await ScalarAsync("SELECT name FROM sqlite_master WHERE type='table' AND name='WorkoutSessions';");
        Assert.Equal("WorkoutSessions", name);
    }

    [Fact]
    public async Task InitializeAsync_CreatesUserSettingsTable()
    {
        var name = await ScalarAsync("SELECT name FROM sqlite_master WHERE type='table' AND name='UserSettings';");
        Assert.Equal("UserSettings", name);
    }

    [Fact]
    public async Task InitializeAsync_CreatesDedupIndex()
    {
        var name = await ScalarAsync("SELECT name FROM sqlite_master WHERE type='index' AND name='UX_WorkoutSessions_SessionDate';");
        Assert.Equal("UX_WorkoutSessions_SessionDate", name);
    }

    [Fact]
    public async Task InitializeAsync_CreatesCoveringIndex()
    {
        var name = await ScalarAsync("SELECT name FROM sqlite_master WHERE type='index' AND name='IX_WorkoutSessions_Date';");
        Assert.Equal("IX_WorkoutSessions_Date", name);
    }

    // ── Seeding ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_SeedsUserSettingsWithKmDefault()
    {
        var unit = await ScalarAsync("SELECT DistanceUnit FROM UserSettings WHERE Id = 1;");
        Assert.Equal("km", unit);
    }

    [Fact]
    public async Task InitializeAsync_SeedsExactlyOneUserSettingsRow()
    {
        var count = await ScalarAsync("SELECT COUNT(*) FROM UserSettings;");
        Assert.Equal(1L, count);
    }

    // ── Idempotency ────────────────────────────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_CalledTwice_DoesNotThrow()
    {
        var ex = await Record.ExceptionAsync(() => _sut.InitializeAsync());
        Assert.Null(ex);
    }

    [Fact]
    public async Task InitializeAsync_CalledTwice_DoesNotDuplicateUserSettingsRow()
    {
        await _sut.InitializeAsync();
        var count = await ScalarAsync("SELECT COUNT(*) FROM UserSettings;");
        Assert.Equal(1L, count);
    }

    // ── Connection factory ─────────────────────────────────────────────────────

    [Fact]
    public void CreateConnection_ReturnsOpenableConnection()
    {
        using var conn = _sut.CreateConnection();
        var ex = Record.Exception(conn.Open);
        Assert.Null(ex);
    }

    [Fact]
    public async Task CreateConnection_MultipleCallsReturnIndependentConnections()
    {
        using var conn1 = _sut.CreateConnection();
        using var conn2 = _sut.CreateConnection();
        await conn1.OpenAsync();
        await conn2.OpenAsync();

        Assert.Equal(ConnectionState.Open, conn1.State);
        Assert.Equal(ConnectionState.Open, conn2.State);
    }

    // ── Helper ─────────────────────────────────────────────────────────────────

    private async Task<object?> ScalarAsync(string sql)
    {
        using var conn = _sut.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return await cmd.ExecuteScalarAsync();
    }
}
