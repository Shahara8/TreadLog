using Npgsql;
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
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO workoutsessions
                (sessiondate, durationseconds, distancekm, avgspeedkmh, inclinepercent,
                 caloriesburned, avgheartrate, createdat, updatedat, programid, completed)
            VALUES
                (@SessionDate, @DurationSeconds, @DistanceKm, @AvgSpeedKmh, @InclinePercent,
                 @CaloriesBurned, @AvgHeartRate, @CreatedAt, @UpdatedAt, @ProgramId, @Completed)
            RETURNING id;", conn);

        BindSessionParams(cmd, session);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task UpdateAsync(WorkoutSession session)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            UPDATE workoutsessions SET
                sessiondate     = @SessionDate,
                durationseconds = @DurationSeconds,
                distancekm      = @DistanceKm,
                avgspeedkmh     = @AvgSpeedKmh,
                inclinepercent  = @InclinePercent,
                caloriesburned  = @CaloriesBurned,
                avgheartrate    = @AvgHeartRate,
                updatedat       = @UpdatedAt,
                programid       = @ProgramId,
                completed       = @Completed
            WHERE id = @Id;", conn);

        BindSessionParams(cmd, session);
        cmd.Parameters.AddWithValue("@Id", session.Id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            "DELETE FROM workoutsessions WHERE id = @Id;", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> BulkInsertIgnoreAsync(IEnumerable<WorkoutSession> sessions)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        await using var tx = await conn.BeginTransactionAsync();
        int inserted = 0;
        try
        {
            foreach (var session in sessions)
            {
                await using var cmd = new NpgsqlCommand(@"
                    INSERT INTO workoutsessions
                        (sessiondate, durationseconds, distancekm, avgspeedkmh, inclinepercent,
                         caloriesburned, avgheartrate, createdat, updatedat, programid, completed)
                    VALUES
                        (@SessionDate, @DurationSeconds, @DistanceKm, @AvgSpeedKmh, @InclinePercent,
                         @CaloriesBurned, @AvgHeartRate, @CreatedAt, @UpdatedAt, @ProgramId, @Completed)
                    ON CONFLICT (sessiondate) DO NOTHING;", conn, tx);
                BindSessionParams(cmd, session);
                inserted += await cmd.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }

        return inserted;
    }

    public async Task<WorkoutSession?> GetByIdAsync(int id)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand($"{SelectSql} WHERE id = @Id;", conn);
        cmd.Parameters.AddWithValue("@Id", id);

        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapRow(reader) : null;
    }

    public async Task<IEnumerable<WorkoutSession>> GetAllAsync()
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand($"{SelectSql} ORDER BY sessiondate DESC;", conn);
        return await ReadListAsync(cmd);
    }

    public async Task<IEnumerable<WorkoutSession>> GetByDateRangeAsync(string startDate, string endDate)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            $"{SelectSql} WHERE sessiondate >= @Start AND sessiondate <= @End ORDER BY sessiondate ASC;", conn);
        cmd.Parameters.AddWithValue("@Start", startDate);
        cmd.Parameters.AddWithValue("@End",   endDate);
        return await ReadListAsync(cmd);
    }

    private const string SelectSql = @"
        SELECT id, sessiondate, durationseconds, distancekm, avgspeedkmh,
               inclinepercent, caloriesburned, avgheartrate, createdat, updatedat,
               programid, completed
        FROM workoutsessions";

    private static void BindSessionParams(NpgsqlCommand cmd, WorkoutSession s)
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
        cmd.Parameters.AddWithValue("@UpdatedAt",  DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("@ProgramId",  (object?)s.ProgramId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Completed",  s.Completed);
    }

    private static WorkoutSession MapRow(NpgsqlDataReader r) => new()
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
        UpdatedAt       = r.GetString(9),
        ProgramId       = r.IsDBNull(10) ? null : r.GetInt32(10),
        Completed       = r.GetBoolean(11)
    };

    private static async Task<List<WorkoutSession>> ReadListAsync(NpgsqlCommand cmd)
    {
        var list = new List<WorkoutSession>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(MapRow(reader));
        return list;
    }
}
