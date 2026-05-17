using Npgsql;
using TreadLog.Api.Services.Interfaces;
using TreadLog.Models;
using TreadLog.Services.Interfaces;

namespace TreadLog.Api.Services;

public class WorkoutProgramRepository : IWorkoutProgramRepository
{
    private readonly IDatabaseService _db;

    public WorkoutProgramRepository(IDatabaseService db) => _db = db;

    public async Task<IEnumerable<WorkoutProgram>> GetAllAsync()
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        var programs = new List<WorkoutProgram>();

        await using var cmd = new NpgsqlCommand(@"
            SELECT id, name, description, createdat, updatedat
            FROM workoutprograms ORDER BY name;", conn);
        await using (var r = await cmd.ExecuteReaderAsync())
            while (await r.ReadAsync())
                programs.Add(MapProgram(r));

        foreach (var p in programs)
            p.Steps = await LoadSteps(conn, p.Id);

        return programs;
    }

    public async Task<WorkoutProgram?> GetByIdAsync(int id)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT id, name, description, createdat, updatedat
            FROM workoutprograms WHERE id = @Id;", conn);
        cmd.Parameters.AddWithValue("@Id", id);

        WorkoutProgram? prog;
        await using (var r = await cmd.ExecuteReaderAsync())
            prog = await r.ReadAsync() ? MapProgram(r) : null;

        if (prog != null)
            prog.Steps = await LoadSteps(conn, prog.Id);

        return prog;
    }

    public async Task<int> AddAsync(WorkoutProgram program)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO workoutprograms (name, description, createdat, updatedat)
                VALUES (@Name, @Description, @Now, @Now)
                RETURNING id;", conn, tx);
            cmd.Parameters.AddWithValue("@Name",        program.Name);
            cmd.Parameters.AddWithValue("@Description", (object?)program.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Now",         DateTime.UtcNow.ToString("o"));
            var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            await InsertSteps(conn, tx, id, program.Steps);
            await tx.CommitAsync();
            return id;
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    public async Task UpdateAsync(WorkoutProgram program)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            await using var upd = new NpgsqlCommand(@"
                UPDATE workoutprograms SET name=@Name, description=@Description, updatedat=@Now
                WHERE id=@Id;", conn, tx);
            upd.Parameters.AddWithValue("@Name",        program.Name);
            upd.Parameters.AddWithValue("@Description", (object?)program.Description ?? DBNull.Value);
            upd.Parameters.AddWithValue("@Now",         DateTime.UtcNow.ToString("o"));
            upd.Parameters.AddWithValue("@Id",          program.Id);
            await upd.ExecuteNonQueryAsync();

            await using var del = new NpgsqlCommand(
                "DELETE FROM workoutprogramsteps WHERE programid=@Id;", conn, tx);
            del.Parameters.AddWithValue("@Id", program.Id);
            await del.ExecuteNonQueryAsync();

            await InsertSteps(conn, tx, program.Id, program.Steps);
            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    public async Task DeleteAsync(int id)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM workoutprograms WHERE id=@Id;", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task InsertSteps(
        NpgsqlConnection conn, NpgsqlTransaction tx, int programId,
        IEnumerable<WorkoutProgramStep> steps)
    {
        int order = 1;
        foreach (var step in steps)
        {
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO workoutprogramsteps (programid, steporder, durationseconds, speedkmh)
                VALUES (@ProgramId, @Order, @Duration, @Speed);", conn, tx);
            cmd.Parameters.AddWithValue("@ProgramId", programId);
            cmd.Parameters.AddWithValue("@Order",     order++);
            cmd.Parameters.AddWithValue("@Duration",  step.DurationSeconds);
            cmd.Parameters.AddWithValue("@Speed",     step.SpeedKmh);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task<List<WorkoutProgramStep>> LoadSteps(NpgsqlConnection conn, int programId)
    {
        var steps = new List<WorkoutProgramStep>();
        await using var cmd = new NpgsqlCommand(@"
            SELECT id, programid, steporder, durationseconds, speedkmh
            FROM workoutprogramsteps WHERE programid=@Id ORDER BY steporder;", conn);
        cmd.Parameters.AddWithValue("@Id", programId);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            steps.Add(new WorkoutProgramStep
            {
                Id              = r.GetInt32(0),
                ProgramId       = r.GetInt32(1),
                StepOrder       = r.GetInt32(2),
                DurationSeconds = r.GetInt32(3),
                SpeedKmh        = r.GetDouble(4)
            });
        return steps;
    }

    private static WorkoutProgram MapProgram(NpgsqlDataReader r) => new()
    {
        Id          = r.GetInt32(0),
        Name        = r.GetString(1),
        Description = r.IsDBNull(2) ? null : r.GetString(2),
        CreatedAt   = r.GetString(3),
        UpdatedAt   = r.GetString(4)
    };
}
