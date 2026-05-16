using Microsoft.Data.Sqlite;

namespace TreadLog.Api.Services.Interfaces;

public interface IDatabaseService
{
    SqliteConnection CreateConnection();
    Task InitializeAsync();
}
