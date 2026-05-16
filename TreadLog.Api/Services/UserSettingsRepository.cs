using TreadLog.Api.Services.Interfaces;
using TreadLog.Models;
using TreadLog.Services.Interfaces;

namespace TreadLog.Api.Services;

public class UserSettingsRepository : IUserSettingsRepository
{
    private readonly IDatabaseService _db;

    public UserSettingsRepository(IDatabaseService db) => _db = db;

    public async Task<UserSettings> GetAsync()
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, DistanceUnit, UpdatedAt FROM UserSettings WHERE Id = 1;";

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new InvalidOperationException("UserSettings row not found.");

        return new UserSettings
        {
            Id           = reader.GetInt32(0),
            DistanceUnit = reader.GetString(1),
            UpdatedAt    = reader.GetString(2)
        };
    }

    public async Task SaveAsync(UserSettings settings)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE UserSettings
            SET DistanceUnit = @DistanceUnit,
                UpdatedAt    = @UpdatedAt
            WHERE Id = 1;";

        cmd.Parameters.AddWithValue("@DistanceUnit", settings.DistanceUnit);
        cmd.Parameters.AddWithValue("@UpdatedAt",    DateTime.UtcNow.ToString("o"));
        await cmd.ExecuteNonQueryAsync();
    }
}
