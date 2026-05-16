using TreadLog.Models;

namespace TreadLog.Services.Interfaces;

public interface IWorkoutRepository
{
    /// <summary>Inserts a new session and returns its generated Id.</summary>
    Task<int> AddAsync(WorkoutSession session);

    /// <summary>Returns the session with the given Id, or null if not found.</summary>
    Task<WorkoutSession?> GetByIdAsync(int id);

    /// <summary>Returns all sessions ordered by SessionDate descending.</summary>
    Task<IEnumerable<WorkoutSession>> GetAllAsync();

    /// <summary>
    /// Returns sessions whose SessionDate falls within [startDate, endDate] (ISO 8601 strings).
    /// Results are ordered by SessionDate ascending.
    /// </summary>
    Task<IEnumerable<WorkoutSession>> GetByDateRangeAsync(string startDate, string endDate);

    /// <summary>Overwrites all editable fields for the session identified by session.Id.</summary>
    Task UpdateAsync(WorkoutSession session);

    /// <summary>Permanently removes the session with the given Id.</summary>
    Task DeleteAsync(int id);

    /// <summary>
    /// Inserts each session that does not already have a matching SessionDate.
    /// Duplicate rows are silently skipped (INSERT OR IGNORE).
    /// Runs inside a single transaction — all-or-nothing on error.
    /// Returns the count of rows actually inserted.
    /// </summary>
    Task<int> BulkInsertIgnoreAsync(IEnumerable<WorkoutSession> sessions);
}
