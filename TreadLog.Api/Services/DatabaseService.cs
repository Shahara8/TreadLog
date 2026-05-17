using Npgsql;
using TreadLog.Api.Services.Interfaces;

namespace TreadLog.Api.Services;

public class DatabaseService : IDatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public NpgsqlConnection CreateConnection() => new(_connectionString);

    public async Task InitializeAsync()
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            await ExecAsync(conn, tx, @"
                CREATE TABLE IF NOT EXISTS workoutsessions (
                    id              SERIAL PRIMARY KEY,
                    sessiondate     TEXT             NOT NULL,
                    durationseconds INTEGER          NOT NULL,
                    distancekm      DOUBLE PRECISION NOT NULL,
                    avgspeedkmh     DOUBLE PRECISION NOT NULL,
                    inclinepercent  DOUBLE PRECISION NOT NULL DEFAULT 0.0,
                    caloriesburned  INTEGER,
                    avgheartrate    INTEGER,
                    createdat       TEXT             NOT NULL DEFAULT (NOW()::TEXT),
                    updatedat       TEXT             NOT NULL DEFAULT (NOW()::TEXT)
                );");

            await ExecAsync(conn, tx, @"
                CREATE UNIQUE INDEX IF NOT EXISTS ux_workoutsessions_sessiondate
                    ON workoutsessions (sessiondate);");

            await ExecAsync(conn, tx, @"
                CREATE INDEX IF NOT EXISTS ix_workoutsessions_date
                    ON workoutsessions (sessiondate, distancekm, durationseconds, avgspeedkmh, inclinepercent);");

            await ExecAsync(conn, tx, @"
                CREATE TABLE IF NOT EXISTS usersettings (
                    id           INTEGER PRIMARY KEY CHECK (id = 1),
                    distanceunit TEXT    NOT NULL DEFAULT 'km',
                    updatedat    TEXT    NOT NULL DEFAULT (NOW()::TEXT)
                );");

            await ExecAsync(conn, tx,
                "INSERT INTO usersettings (id, distanceunit) VALUES (1, 'km') ON CONFLICT (id) DO NOTHING;");

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private static async Task ExecAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        await cmd.ExecuteNonQueryAsync();
    }
}
