using Npgsql;

namespace TreadLog.Api.Services.Interfaces;

public interface IDatabaseService
{
    NpgsqlConnection CreateConnection();
    Task InitializeAsync();
}
