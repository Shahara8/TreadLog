namespace TreadLog.Helpers;

/// <summary>
/// Stateless validation and auto-calculation utilities for workout metrics.
/// All methods are pure functions — no database access, no UI concerns.
/// The ViewModel layer is responsible for deciding which metric to recalculate
/// based on which field the user last edited.
/// </summary>
public static class WorkoutValidator
{
    /// <summary>
    /// Relative tolerance for the distance/speed/duration consistency check.
    /// 0.02 = 2 %: accounts for display rounding (e.g. speed shown to 1 d.p., distance to 2 d.p.)
    /// while still flagging physically impossible combinations like 10 km at 5 km/h in 10 minutes.
    /// </summary>
    public const double ConsistencyTolerance = 0.02;

    // Domain constants sourced from the PRD field specification
    public const double MaxTreadmillSpeedKmh = 50.0;
    public const double MaxInclinePercent    = 15.0;
    public const int    MinHeartRateBpm      = 40;
    public const int    MaxHeartRateBpm      = 220;

    // ── Individual field validators ───────────────────────────────────────────

    /// <summary>Duration must be at least one second.</summary>
    public static bool IsDurationValid(int durationSeconds)
        => durationSeconds > 0;

    /// <summary>Distance must be strictly positive (zero-distance sessions are not meaningful).</summary>
    public static bool IsDistanceValid(double distanceKm)
        => distanceKm > 0;

    /// <summary>Speed must be positive and within the physical treadmill ceiling.</summary>
    public static bool IsSpeedValid(double avgSpeedKmh)
        => avgSpeedKmh > 0 && avgSpeedKmh <= MaxTreadmillSpeedKmh;

    /// <summary>Incline percentage must fall within the 0–15 % hardware range.</summary>
    public static bool IsInclineValid(double inclinePercent)
        => inclinePercent >= 0 && inclinePercent <= MaxInclinePercent;

    /// <summary>Heart rate is optional; when supplied it must be in the physiological range.</summary>
    public static bool IsHeartRateValid(int? bpm)
        => bpm is null or (>= MinHeartRateBpm and <= MaxHeartRateBpm);

    /// <summary>Calories are optional; when supplied they must be a positive integer.</summary>
    public static bool IsCaloriesValid(int? calories)
        => calories is null or > 0;

    // ── Cross-field consistency ───────────────────────────────────────────────

    /// <summary>
    /// Returns true when the three metrics satisfy Distance ≈ Speed × Duration
    /// within <see cref="ConsistencyTolerance"/> (2 % relative error).
    ///
    /// Returns false — not an exception — for any non-positive or zero input so the
    /// ViewModel can show inline validation without a try/catch wrapper.
    /// </summary>
    public static bool IsPhysicallyConsistent(double distanceKm, double avgSpeedKmh, int durationSeconds)
    {
        if (distanceKm <= 0 || avgSpeedKmh <= 0 || durationSeconds <= 0)
            return false;

        double expected      = CalculateDistance(avgSpeedKmh, durationSeconds);
        double relativeError = Math.Abs(distanceKm - expected) / expected;
        return relativeError <= ConsistencyTolerance;
    }

    // ── Auto-calculation helpers ──────────────────────────────────────────────

    /// <summary>
    /// Given speed (km/h) and duration (seconds), returns the implied distance in km.
    /// Returns 0 for any non-positive input — never throws.
    /// </summary>
    public static double CalculateDistance(double avgSpeedKmh, int durationSeconds)
    {
        if (avgSpeedKmh <= 0 || durationSeconds <= 0)
            return 0;

        return avgSpeedKmh * (durationSeconds / 3600.0);
    }

    /// <summary>
    /// Given distance (km) and speed (km/h), returns the implied duration in whole seconds.
    /// Returns 0 for any non-positive input — never throws.
    /// </summary>
    public static int CalculateDuration(double distanceKm, double avgSpeedKmh)
    {
        if (distanceKm <= 0 || avgSpeedKmh <= 0)
            return 0;

        return (int)Math.Round(distanceKm / avgSpeedKmh * 3600.0);
    }

    /// <summary>
    /// Given distance (km) and duration (seconds), returns the implied average speed in km/h.
    /// Returns 0 for any non-positive input — never throws.
    /// </summary>
    public static double CalculateSpeed(double distanceKm, int durationSeconds)
    {
        if (distanceKm <= 0 || durationSeconds <= 0)
            return 0;

        return distanceKm / (durationSeconds / 3600.0);
    }
}
