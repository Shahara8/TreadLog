using TreadLog.Models;

namespace TreadLog.Services.Interfaces;

public interface IDataPortabilityService
{
    /// <summary>Serialises all sessions to a CSV file at the given path.</summary>
    Task ExportToCsvAsync(IEnumerable<WorkoutSession> sessions, string filePath);

    /// <summary>Serialises all sessions to a JSON file at the given path.</summary>
    Task ExportToJsonAsync(IEnumerable<WorkoutSession> sessions, string filePath);

    /// <summary>
    /// Parses the file at <paramref name="filePath"/>, validates the schema,
    /// and appends non-duplicate rows via <see cref="IWorkoutRepository.BulkInsertIgnoreAsync"/>.
    /// </summary>
    Task<ImportResult> ImportFromFileAsync(string filePath);
}

/// <summary>Summary returned to the UI after an import operation.</summary>
public sealed record ImportResult(
    int TotalRows,
    int InsertedRows,
    int SkippedRows,
    IReadOnlyList<string> Errors);
