using Microsoft.Data.Sqlite;
using TreadLog.Api.Services.Interfaces;
using TreadLog.Models;
using TreadLog.Services.Interfaces;

namespace TreadLog.Api.Services;

public class WorkoutRepository : IWorkoutRepository
{
    private readonly IDatabaseService _db;

    public WorkoutRepository(IDatabaseService db) => _db = db;

    public async Task<int> AddAsync(WorkoutSession session)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO WorkoutSessions
                (SessionDate, DurationSeconds, DistanceKm, AvgSpeedKmh, InclinePercent,
                 CaloriesBurned, AvgHeartRate, CreatedAt, UpdatedAt)
            VALUES
                (@SessionDate, @DurationSeconds, @DistanceKm, @AvgSpeedKmh, @InclinePercent,
                 @CaloriesBurned, @AvgHeartRate, @CreatedAt, @UpdatedAt);
            SELECT last_insert_rowid();";

        BindSessionParams(cmd, session);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task UpdateAsync(WorkoutSession session)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE WorkoutSessions SET
                SessionDate     = @SessionDate,
                DurationSeconds = @DurationSeconds,
                DistanceKm      = @DistanceKm,
                AvgSpeedKmh     = @AvgSpeedKmh,
                InclinePercent  = @InclinePercent,
                CaloriesBurned  = @CaloriesBurned,
                AvgHeartRate    = @AvgHeartRate,
                UpdatedAt       = @UpdatedAt
            WHERE Id = @Id;";

        BindSessionParams(cmd, session);
        cmd.Parameters.AddWithValue("@Id", session.Id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM WorkoutSessions WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> BulkInsertIgnoreAsync(IEnumerable<WorkoutSession> sessions)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        using var tx = conn.BeginTransaction();
        int inserted = 0;
        try
        {
            foreach (var session in sessions)
            {
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT OR IGNORE INTO WorkoutSessions
                        (SessionDate, DurationSeconds, DistanceKm, AvgSpeedKmh, InclinePercent,
                         CaloriesBurned, AvgHeartRate, CreatedAt, UpdatedAt)
                    VALUES
                        (@SessionDate, @DurationSeconds, @DistanceKm, @AvgSpeedKmh, @InclinePercent,
                         @CaloriesBurned, @AvgHeartRate, @CreatedAt, @UpdatedAt);";
                BindSessionParams(cmd, session);
                inserted += await cmd.ExecuteNonQueryAsync();
            }
            tx.Commit();
        }
        catch { tx.Rollback(); throw; }

        return inserted;
    }

    public async Task<WorkoutSession?> GetByIdAsync(int id)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"{SelectSql} WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", id);

        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapRow(reader) : null;
    }

    public async Task<IEnumerable<WorkoutSession>> GetAllAsync()
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"{SelectSql} ORDER BY SessionDate DESC;";
        return await ReadListAsync(cmd);
    }

    public async Task<IEnumerable<WorkoutSession>> GetByDateRangeAsync(string startDate, string endDate)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"{SelectSql} WHERE SessionDate >= @Start AND SessionDate <= @End ORDER BY SessionDate ASC;";
        cmd.Parameters.AddWithValue("@Start", startDate);
        cmd.Parameters.AddWithValue("@End",   endDate);
        return await ReadListAsync(cmd);
    }

    private const string SelectSql = @"
        SELECT Id, SessionDate, DurationSeconds, DistanceKm, AvgSpeedKmh,
               InclinePercent, CaloriesBurned, AvgHeartRate, CreatedAt, UpdatedAt
        FROM WorkoutSessions";

    private static void BindSessionParams(SqliteCommand cmd, WorkoutSession s)
    {
        cmd.Parameters.AddWithValue("@SessionDate",     s.SessionDate.ToUniversalTime().ToString("o"));
        cmd.Parameters.AddWithValue("@DurationSeconds", s.DurationSeconds);
        cmd.Parameters.AddWithValue("@DistanceKm",      s.DistanceKm);
        cmd.Parameters.AddWithValue("@AvgSpeedKmh",     s.AvgSpeedKmh);
        cmd.Parameters.AddWithValue("@InclinePercent",  s.InclinePercent);
        cmd.Parameters.AddWithValue("@CaloriesBurned",  (object?)s.CaloriesBurned ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AvgHeartRate",    (object?)s.AvgHeartRate   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt",
            string.IsNullOrEmpty(s.CreatedAt) ? DateTime.UtcNow.ToString("o") : s.CreatedAt);
        cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow.ToString("o"));
    }

    private static WorkoutSession MapRow(SqliteDataReader r) => new()
    {
        Id              = r.GetInt32(0),
        SessionDate     = DateTime.Parse(r.GetString(1),
                              null, System.Globalization.DateTimeStyles.RoundtripKind),
        DurationSeconds = r.GetInt32(2),
        DistanceKm      = r.GetDouble(3),
        AvgSpeedKmh     = r.GetDouble(4),
        InclinePercent  = r.GetDouble(5),
        CaloriesBurned  = r.IsDBNull(6) ? null : r.GetInt32(6),
        AvgHeartRate    = r.IsDBNull(7) ? null : r.GetInt32(7),
        CreatedAt       = r.GetString(8),
        UpdatedAt       = r.GetString(9)
    };

    private static async Task<List<WorkoutSession>> ReadListAsync(SqliteCommand cmd)
    {
        var list = new List<WorkoutSession>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(MapRow(reader));
        return list;
    }
}
