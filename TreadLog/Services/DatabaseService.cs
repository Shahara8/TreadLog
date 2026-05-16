using Microsoft.Data.Sqlite;
using TreadLog.Services.Interfaces;

namespace TreadLog.Services;

public class DatabaseService : IDatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(string dbPath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode       = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    public SqliteConnection CreateConnection() => new(_connectionString);

    public async Task InitializeAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();
        try
        {
            await CreateWorkoutSessionsTableAsync(conn, tx);
            await CreateUserSettingsTableAsync(conn, tx);
            await SeedUserSettingsAsync(conn, tx);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    // ── DDL helpers ────────────────────────────────────────────────────────────

    private static async Task CreateWorkoutSessionsTableAsync(SqliteConnection conn, SqliteTransaction tx)
    {
        await ExecAsync(conn, tx, @"
            CREATE TABLE IF NOT EXISTS WorkoutSessions (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                SessionDate     TEXT    NOT NULL,
                DurationSeconds INTEGER NOT NULL,
                DistanceKm      REAL    NOT NULL,
                AvgSpeedKmh     REAL    NOT NULL,
                InclinePercent  REAL    NOT NULL DEFAULT 0.0,
                CaloriesBurned  INTEGER,
                AvgHeartRate    INTEGER,
                CreatedAt       TEXT    NOT NULL DEFAULT (datetime('now')),
                UpdatedAt       TEXT    NOT NULL DEFAULT (datetime('now'))
            );");

        // Unique index drives the import dedup via INSERT OR IGNORE
        await ExecAsync(conn, tx, @"
            CREATE UNIQUE INDEX IF NOT EXISTS UX_WorkoutSessions_SessionDate
                ON WorkoutSessions (SessionDate);");

        // Covering index keeps dashboard aggregate queries under 100 ms for 10 k rows
        await ExecAsync(conn, tx, @"
            CREATE INDEX IF NOT EXISTS IX_WorkoutSessions_Date
                ON WorkoutSessions (SessionDate, DistanceKm, DurationSeconds, AvgSpeedKmh, InclinePercent);");
    }

    private static async Task CreateUserSettingsTableAsync(SqliteConnection conn, SqliteTransaction tx)
    {
        await ExecAsync(conn, tx, @"
            CREATE TABLE IF NOT EXISTS UserSettings (
                Id           INTEGER PRIMARY KEY CHECK (Id = 1),
                DistanceUnit TEXT    NOT NULL DEFAULT 'km',
                UpdatedAt    TEXT    NOT NULL DEFAULT (datetime('now'))
            );");
    }

    private static async Task SeedUserSettingsAsync(SqliteConnection conn, SqliteTransaction tx)
    {
        await ExecAsync(conn, tx,
            "INSERT OR IGNORE INTO UserSettings (Id, DistanceUnit) VALUES (1, 'km');");
    }

    private static async Task ExecAsync(SqliteConnection conn, SqliteTransaction tx, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction  = tx;
        cmd.CommandText  = sql;
        await cmd.ExecuteNonQueryAsync();
    }
}
