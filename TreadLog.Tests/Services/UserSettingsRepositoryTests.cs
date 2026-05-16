using TreadLog.Models;
using TreadLog.Services;

namespace TreadLog.Tests.Services;

public class UserSettingsRepositoryTests : IAsyncLifetime
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"tl_settings_{Guid.NewGuid():N}.db");

    private DatabaseService         _dbService = null!;
    private UserSettingsRepository  _sut       = null!;

    public async Task InitializeAsync()
    {
        _dbService = new DatabaseService(_dbPath);
        await _dbService.InitializeAsync();
        _sut = new UserSettingsRepository(_dbService);
    }

    public async Task DisposeAsync()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        await Task.Delay(20);
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* temp file — OS cleanup */ }
    }

    // ── GetAsync ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_AfterInit_ReturnsSeededRow()
    {
        var settings = await _sut.GetAsync();

        Assert.NotNull(settings);
        Assert.Equal(1,    settings.Id);
        Assert.Equal("km", settings.DistanceUnit);
    }

    [Fact]
    public async Task GetAsync_UpdatedAt_IsNonEmpty()
    {
        var settings = await _sut.GetAsync();
        Assert.False(string.IsNullOrEmpty(settings.UpdatedAt));
    }

    [Fact]
    public async Task GetAsync_UpdatedAt_IsValidIso8601()
    {
        var settings = await _sut.GetAsync();
        var parsed   = DateTime.TryParse(settings.UpdatedAt, out _);
        Assert.True(parsed);
    }

    // ── SaveAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_ChangingDistanceUnit_PersistsOnNextRead()
    {
        var settings = await _sut.GetAsync();
        settings.DistanceUnit = "mi";

        await _sut.SaveAsync(settings);
        var reloaded = await _sut.GetAsync();

        Assert.Equal("mi", reloaded.DistanceUnit);
    }

    [Fact]
    public async Task SaveAsync_SwitchingBackToKm_PersistsOnNextRead()
    {
        var settings = await _sut.GetAsync();
        settings.DistanceUnit = "mi";
        await _sut.SaveAsync(settings);

        settings.DistanceUnit = "km";
        await _sut.SaveAsync(settings);

        Assert.Equal("km", (await _sut.GetAsync()).DistanceUnit);
    }

    [Fact]
    public async Task SaveAsync_UpdatedAt_IsStampedWithCurrentUtcTime()
    {
        var before   = DateTime.UtcNow.AddSeconds(-1);
        var settings = await _sut.GetAsync();

        await _sut.SaveAsync(settings);
        var reloaded  = await _sut.GetAsync();
        var updatedAt = DateTime.Parse(reloaded.UpdatedAt,
                            null, System.Globalization.DateTimeStyles.RoundtripKind);

        Assert.True(updatedAt >= before);
        Assert.True(updatedAt <= DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task SaveAsync_DoesNotCreateExtraRows()
    {
        var settings = await _sut.GetAsync();
        await _sut.SaveAsync(settings);
        await _sut.SaveAsync(settings);

        // Must still be exactly one row — CHECK (Id = 1) prevents duplicates
        using var conn = _dbService.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM UserSettings;";
        var count = await cmd.ExecuteScalarAsync();
        Assert.Equal(1L, count);
    }
}
