namespace TreadLog.Models;

public class UserSettings
{
    /// <summary>Always 1 — enforced by the DB CHECK constraint to guarantee a single row.</summary>
    public int Id { get; set; } = 1;

    /// <summary>"km" or "mi" — controls display conversion throughout the UI.</summary>
    public string DistanceUnit { get; set; } = "km";

    /// <summary>Last-modified timestamp stored verbatim as ISO 8601 TEXT from SQLite.</summary>
    public string UpdatedAt { get; set; } = string.Empty;
}
