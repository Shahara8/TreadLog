namespace TreadLog.Models;

public class WorkoutSession
{
    public int Id { get; set; }

    /// <summary>ISO 8601 workout start timestamp — dedup anchor for import.</summary>
    public DateTime SessionDate { get; set; }

    /// <summary>Total elapsed time stored as whole seconds.</summary>
    public int DurationSeconds { get; set; }

    /// <summary>Distance always stored in kilometres regardless of UI unit preference.</summary>
    public double DistanceKm { get; set; }

    /// <summary>Average speed always stored in km/h regardless of UI unit preference.</summary>
    public double AvgSpeedKmh { get; set; }

    /// <summary>Treadmill incline as a percentage (0.0 – 15.0).</summary>
    public double InclinePercent { get; set; }

    public int? CaloriesBurned { get; set; }

    /// <summary>Valid range 40 – 220 BPM; null when not recorded.</summary>
    public int? AvgHeartRate { get; set; }

    /// <summary>Row insertion timestamp stored verbatim as ISO 8601 TEXT from SQLite.</summary>
    public string CreatedAt { get; set; } = string.Empty;

    /// <summary>Last-modified timestamp stored verbatim as ISO 8601 TEXT from SQLite.</summary>
    public string UpdatedAt { get; set; } = string.Empty;

    /// <summary>Program used for this session; null means free run.</summary>
    public int? ProgramId { get; set; }

    /// <summary>True if the full program was completed; false if stopped early.</summary>
    public bool Completed { get; set; } = true;
}
