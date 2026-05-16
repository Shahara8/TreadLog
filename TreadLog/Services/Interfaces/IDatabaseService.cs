using Microsoft.Data.Sqlite;

namespace TreadLog.Services.Interfaces;

public interface IDatabaseService
{
    /// <summary>Opens a new connection to the configured database file.</summary>
    SqliteConnection CreateConnection();

    /// <summary>
    /// Creates all tables, indexes, and seeds the UserSettings row on first run.
    /// Safe to call multiple times — all DDL uses IF NOT EXISTS / INSERT OR IGNORE.
    /// </summary>
    Task InitializeAsync();
}
