namespace TreadLog.Helpers;

/// <summary>
/// Stateless validation and auto-calculation utilities for workout metrics.
/// All methods are pure functions — no database access, no UI concerns.
/// </summary>
public static class WorkoutValidator
{
    /// <summary>
    /// Relative tolerance for the distance/speed/duration consistency check.
    /// 0.02 = 2 %: accounts for display rounding while still flagging impossible combinations.
    /// </summary>
    public const double ConsistencyTolerance = 0.02;

    public const double MaxTreadmillSpeedKmh = 50.0;
    public const double MaxInclinePercent    = 15.0;
    public const int    MinHeartRateBpm      = 40;
    public const int    MaxHeartRateBpm      = 220;

    // ── Individual field validators ───────────────────────────────────────────

    public static bool IsDurationValid(int durationSeconds)
        => durationSeconds > 0;

    public static bool IsDistanceValid(double distanceKm)
        => distanceKm > 0;

    public static bool IsSpeedValid(double avgSpeedKmh)
        => avgSpeedKmh > 0 && avgSpeedKmh <= MaxTreadmillSpeedKmh;

    public static bool IsInclineValid(double inclinePercent)
        => inclinePercent >= 0 && inclinePercent <= MaxInclinePercent;

    public static bool IsHeartRateValid(int? bpm)
        => bpm is null or (>= MinHeartRateBpm and <= MaxHeartRateBpm);

    public static bool IsCaloriesValid(int? calories)
        => calories is null or > 0;

    // ── Cross-field consistency ───────────────────────────────────────────────

    /// <summary>
    /// Returns true when Distance ≈ Speed × Duration within <see cref="ConsistencyTolerance"/>.
    /// Returns false for any non-positive input.
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

    public static double CalculateDistance(double avgSpeedKmh, int durationSeconds)
    {
        if (avgSpeedKmh <= 0 || durationSeconds <= 0)
            return 0;

        return avgSpeedKmh * (durationSeconds / 3600.0);
    }

    public static int CalculateDuration(double distanceKm, double avgSpeedKmh)
    {
        if (distanceKm <= 0 || avgSpeedKmh <= 0)
            return 0;

        return (int)Math.Round(distanceKm / avgSpeedKmh * 3600.0);
    }

    public static double CalculateSpeed(double distanceKm, int durationSeconds)
    {
        if (distanceKm <= 0 || durationSeconds <= 0)
            return 0;

        return distanceKm / (durationSeconds / 3600.0);
    }
}
