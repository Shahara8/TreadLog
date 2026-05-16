namespace TreadLog.Helpers;

/// <summary>
/// Stateless unit-conversion utilities. All public methods are pure functions with no side effects.
/// The canonical internal representation is metric (km, km/h); this class is the only place
/// conversions are performed — never inline in a ViewModel or View.
/// </summary>
public static class UnitConverter
{
    // One international nautical mile = 1.852 km; one statute mile = 1.60934 km (NIST)
    private const double KmPerMile  = 1.60934;
    private const double MilesPerKm = 1.0 / KmPerMile; // ≈ 0.621371

    // ── Distance ──────────────────────────────────────────────────────────────

    public static double KmToMiles(double km)     => km    * MilesPerKm;
    public static double MilesToKm(double miles)  => miles * KmPerMile;

    // ── Speed ─────────────────────────────────────────────────────────────────

    public static double KmhToMph(double kmh) => kmh * MilesPerKm;
    public static double MphToKmh(double mph) => mph * KmPerMile;

    // ── Pace ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Computes pace as a TimeSpan (seconds per unit distance).
    /// Returns TimeSpan.Zero — not an exception — for any degenerate input (zero, negative,
    /// NaN, or infinity). The caller must treat TimeSpan.Zero as "uncalculable".
    /// </summary>
    /// <param name="distanceKm">Total distance in kilometres (internal storage unit).</param>
    /// <param name="durationSeconds">Total elapsed time in seconds.</param>
    /// <param name="unit">"km" (default) or "mi" — determines the denominator unit.</param>
    public static TimeSpan CalculatePace(double distanceKm, double durationSeconds, string unit = "km")
    {
        if (!IsFinitePositive(distanceKm) || !IsFinitePositive(durationSeconds))
            return TimeSpan.Zero;

        double effectiveDistance = unit == "mi" ? KmToMiles(distanceKm) : distanceKm;

        if (!IsFinitePositive(effectiveDistance))
            return TimeSpan.Zero;

        return TimeSpan.FromSeconds(durationSeconds / effectiveDistance);
    }

    /// <summary>
    /// Formats a pace TimeSpan as "M:SS" (e.g. "6:05", "10:30").
    /// Returns "--:--" for zero or negative pace (signals an uncalculable state to the UI).
    /// Minutes are not zero-padded; seconds always use two digits.
    /// </summary>
    public static string FormatPace(TimeSpan pace)
    {
        if (pace <= TimeSpan.Zero)
            return "--:--";

        int totalMinutes = (int)pace.TotalMinutes;
        int seconds      = pace.Seconds;

        return $"{totalMinutes}:{seconds:D2}";
    }

    /// <summary>
    /// Convenience wrapper — calculates pace and returns the formatted display string in one call.
    /// Returns "--:--" when pace cannot be computed.
    /// </summary>
    public static string CalculatePaceString(double distanceKm, double durationSeconds, string unit = "km")
        => FormatPace(CalculatePace(distanceKm, durationSeconds, unit));

    // ── Private guard ─────────────────────────────────────────────────────────

    private static bool IsFinitePositive(double v)
        => !double.IsNaN(v) && !double.IsInfinity(v) && v > 0;
}
