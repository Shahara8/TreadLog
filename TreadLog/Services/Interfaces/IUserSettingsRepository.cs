using TreadLog.Models;

namespace TreadLog.Services.Interfaces;

public interface IUserSettingsRepository
{
    /// <summary>Loads the single UserSettings row (Id = 1). Always returns a value after InitializeAsync.</summary>
    Task<UserSettings> GetAsync();

    /// <summary>Persists DistanceUnit and stamps UpdatedAt with the current UTC time.</summary>
    Task SaveAsync(UserSettings settings);
}
