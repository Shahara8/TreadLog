using TreadLog.Helpers;
using Xunit;

namespace TreadLog.Tests.Helpers;

public class WorkoutValidatorTests
{
    // ── IsDurationValid ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(1,    true)]
    [InlineData(3600, true)]
    [InlineData(0,    false)]
    [InlineData(-1,   false)]
    public void IsDurationValid_Boundary(int seconds, bool expected)
        => Assert.Equal(expected, WorkoutValidator.IsDurationValid(seconds));

    // ── IsDistanceValid ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(0.01,  true)]
    [InlineData(1.0,   true)]
    [InlineData(42.2,  true)]
    [InlineData(0.0,   false)]
    [InlineData(-0.01, false)]
    public void IsDistanceValid_Boundary(double km, bool expected)
        => Assert.Equal(expected, WorkoutValidator.IsDistanceValid(km));

    // ── IsSpeedValid ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0.1,  true)]
    [InlineData(10.0, true)]
    [InlineData(50.0, true)]   // at ceiling
    [InlineData(50.1, false)]  // above ceiling
    [InlineData(0.0,  false)]
    [InlineData(-1.0, false)]
    public void IsSpeedValid_Boundary(double kmh, bool expected)
        => Assert.Equal(expected, WorkoutValidator.IsSpeedValid(kmh));

    // ── IsInclineValid ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0.0,   true)]   // floor
    [InlineData(7.5,   true)]
    [InlineData(15.0,  true)]   // ceiling
    [InlineData(-0.1,  false)]
    [InlineData(15.1,  false)]
    public void IsInclineValid_Boundary(double percent, bool expected)
        => Assert.Equal(expected, WorkoutValidator.IsInclineValid(percent));

    // ── IsHeartRateValid ──────────────────────────────────────────────────────

    [Fact]
    public void IsHeartRateValid_Null_ReturnsTrue()
        => Assert.True(WorkoutValidator.IsHeartRateValid(null));

    [Theory]
    [InlineData(40,  true)]   // floor
    [InlineData(145, true)]
    [InlineData(220, true)]   // ceiling
    [InlineData(39,  false)]
    [InlineData(221, false)]
    public void IsHeartRateValid_NullableBoundary(int bpm, bool expected)
        => Assert.Equal(expected, WorkoutValidator.IsHeartRateValid(bpm));

    // ── IsCaloriesValid ───────────────────────────────────────────────────────

    [Fact]
    public void IsCaloriesValid_Null_ReturnsTrue()
        => Assert.True(WorkoutValidator.IsCaloriesValid(null));

    [Theory]
    [InlineData(1,    true)]
    [InlineData(500,  true)]
    [InlineData(0,    false)]
    [InlineData(-1,   false)]
    public void IsCaloriesValid_Boundary(int calories, bool expected)
        => Assert.Equal(expected, WorkoutValidator.IsCaloriesValid(calories));

    // ── IsPhysicallyConsistent — perfect matches ───────────────────────────────

    [Theory]
    [InlineData(10.0, 10.0, 3600)]   // 10 km/h × 1 h = 10 km
    [InlineData(5.0,  10.0, 1800)]   // 10 km/h × 0.5 h = 5 km
    [InlineData(0.5,  6.0,  300)]    // 6 km/h × 5 min = 0.5 km
    [InlineData(21.1, 10.0, 7596)]   // half-marathon at 10 km/h
    public void IsPhysicallyConsistent_PerfectMatch_ReturnsTrue(
        double distanceKm, double speedKmh, int durationSec)
    {
        Assert.True(WorkoutValidator.IsPhysicallyConsistent(distanceKm, speedKmh, durationSec));
    }

    // ── IsPhysicallyConsistent — within tolerance ──────────────────────────────

    [Theory]
    [InlineData(10.19, 10.0, 3600)]  // +1.9 % — just inside 2 % tolerance
    [InlineData(9.81,  10.0, 3600)]  // -1.9 % — just inside 2 % tolerance
    [InlineData(10.0,  10.0, 3600)]  // exact
    public void IsPhysicallyConsistent_WithinTolerance_ReturnsTrue(
        double distanceKm, double speedKmh, int durationSec)
    {
        Assert.True(WorkoutValidator.IsPhysicallyConsistent(distanceKm, speedKmh, durationSec));
    }

    // ── IsPhysicallyConsistent — at exact tolerance boundary ──────────────────

    [Fact]
    public void IsPhysicallyConsistent_ExactlyAtTolerance_ReturnsTrue()
    {
        // expected = 10 km; 2 % above = 10.2 km → relative error = 0.02 ≤ 0.02
        Assert.True(WorkoutValidator.IsPhysicallyConsistent(10.2, 10.0, 3600));
    }

    [Fact]
    public void IsPhysicallyConsistent_JustAboveTolerance_ReturnsFalse()
    {
        // 10.21 km: relative error ≈ 0.021 > 0.02
        Assert.False(WorkoutValidator.IsPhysicallyConsistent(10.21, 10.0, 3600));
    }

    // ── IsPhysicallyConsistent — clearly impossible values ─────────────────────

    [Fact]
    public void IsPhysicallyConsistent_PrdExample_ImpossibleCombo_ReturnsFalse()
    {
        // PRD example: 10 km in 10 minutes at 5 km/h — physically impossible
        // Expected: 5 × (600/3600) = 0.833 km; actual 10 km → ~1100 % error
        Assert.False(WorkoutValidator.IsPhysicallyConsistent(10.0, 5.0, 600));
    }

    [Theory]
    [InlineData(10.3, 10.0, 3600)]   // 3 % over
    [InlineData(20.0, 10.0, 3600)]   // 100 % over
    [InlineData(5.0,  10.0, 3600)]   // 50 % under
    public void IsPhysicallyConsistent_OutsideTolerance_ReturnsFalse(
        double distanceKm, double speedKmh, int durationSec)
    {
        Assert.False(WorkoutValidator.IsPhysicallyConsistent(distanceKm, speedKmh, durationSec));
    }

    // ── IsPhysicallyConsistent — degenerate inputs ─────────────────────────────

    [Theory]
    [InlineData(0.0,  10.0, 3600)]   // zero distance
    [InlineData(10.0,  0.0, 3600)]   // zero speed
    [InlineData(10.0, 10.0,    0)]   // zero duration
    [InlineData(-1.0, 10.0, 3600)]   // negative distance
    [InlineData(10.0, -1.0, 3600)]   // negative speed
    [InlineData(10.0, 10.0,   -1)]   // negative duration
    public void IsPhysicallyConsistent_DegenerateInputs_ReturnsFalse(
        double distanceKm, double speedKmh, int durationSec)
    {
        Assert.False(WorkoutValidator.IsPhysicallyConsistent(distanceKm, speedKmh, durationSec));
    }

    // ── CalculateDistance ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(10.0, 3600,  10.0)]   // 10 km/h for 1 h
    [InlineData(10.0, 1800,   5.0)]   // 10 km/h for 30 min
    [InlineData(6.0,   300,   0.5)]   // 6 km/h for 5 min
    [InlineData(12.0,  900,   3.0)]   // 12 km/h for 15 min
    public void CalculateDistance_NormalInputs_MatchExpected(
        double speedKmh, int durationSec, double expectedKm)
    {
        Assert.Equal(expectedKm, WorkoutValidator.CalculateDistance(speedKmh, durationSec), 5);
    }

    [Theory]
    [InlineData(0.0,  3600)]   // zero speed
    [InlineData(10.0,    0)]   // zero duration
    [InlineData(-1.0, 3600)]   // negative speed
    [InlineData(10.0,   -1)]   // negative duration
    public void CalculateDistance_DegenerateInputs_ReturnZero(double speedKmh, int durationSec)
        => Assert.Equal(0.0, WorkoutValidator.CalculateDistance(speedKmh, durationSec));

    [Fact]
    public void CalculateDistance_VeryHighSpeed_DoesNotThrow()
    {
        var ex = Record.Exception(() => WorkoutValidator.CalculateDistance(50.0, 3600));
        Assert.Null(ex);
    }

    // ── CalculateDuration ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(10.0, 10.0, 3600)]   // 10 km at 10 km/h = 1 h
    [InlineData(5.0,  10.0, 1800)]   // 5 km at 10 km/h = 30 min
    [InlineData(0.5,   6.0,  300)]   // 0.5 km at 6 km/h = 5 min
    public void CalculateDuration_NormalInputs_MatchExpected(
        double distanceKm, double speedKmh, int expectedSec)
    {
        Assert.Equal(expectedSec, WorkoutValidator.CalculateDuration(distanceKm, speedKmh));
    }

    [Theory]
    [InlineData(0.0,  10.0)]   // zero distance
    [InlineData(10.0,  0.0)]   // zero speed
    [InlineData(-1.0, 10.0)]   // negative distance
    [InlineData(10.0, -1.0)]   // negative speed
    public void CalculateDuration_DegenerateInputs_ReturnZero(double distanceKm, double speedKmh)
        => Assert.Equal(0, WorkoutValidator.CalculateDuration(distanceKm, speedKmh));

    [Fact]
    public void CalculateDuration_FractionalHours_RoundsToNearestSecond()
    {
        // 1 km at 3 km/h = 1200 s exactly
        Assert.Equal(1200, WorkoutValidator.CalculateDuration(1.0, 3.0));
    }

    // ── CalculateSpeed ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(10.0, 3600, 10.0)]   // 10 km in 1 h = 10 km/h
    [InlineData(5.0,  1800, 10.0)]   // 5 km in 30 min = 10 km/h
    [InlineData(0.5,   300,  6.0)]   // 0.5 km in 5 min = 6 km/h
    [InlineData(42.2, 7596, 20.0)]   // half-marathon at 20 km/h (approximately)
    public void CalculateSpeed_NormalInputs_MatchExpected(
        double distanceKm, int durationSec, double expectedKmh)
    {
        Assert.Equal(expectedKmh, WorkoutValidator.CalculateSpeed(distanceKm, durationSec), 3);
    }

    [Theory]
    [InlineData(0.0,  3600)]   // zero distance
    [InlineData(10.0,    0)]   // zero duration
    [InlineData(-1.0, 3600)]   // negative distance
    [InlineData(10.0,   -1)]   // negative duration
    public void CalculateSpeed_DegenerateInputs_ReturnZero(double distanceKm, int durationSec)
        => Assert.Equal(0.0, WorkoutValidator.CalculateSpeed(distanceKm, durationSec));

    // ── Auto-calc round-trips ─────────────────────────────────────────────────

    [Theory]
    [InlineData(10.0, 10.0, 3600)]
    [InlineData(5.0,  8.0,  2250)]
    [InlineData(0.5,  6.0,   300)]
    public void AutoCalc_DistanceFromSpeedAndDuration_IsConsistentWithValidator(
        double distanceKm, double speedKmh, int durationSec)
    {
        double computed = WorkoutValidator.CalculateDistance(speedKmh, durationSec);
        Assert.True(WorkoutValidator.IsPhysicallyConsistent(computed, speedKmh, durationSec));
    }

    [Theory]
    [InlineData(10.0, 10.0, 3600)]
    [InlineData(5.0,  8.0,  2250)]
    public void AutoCalc_DurationFromDistanceAndSpeed_IsConsistentWithValidator(
        double distanceKm, double speedKmh, int durationSec)
    {
        int computed = WorkoutValidator.CalculateDuration(distanceKm, speedKmh);
        Assert.True(WorkoutValidator.IsPhysicallyConsistent(distanceKm, speedKmh, computed));
    }

    [Theory]
    [InlineData(10.0, 10.0, 3600)]
    [InlineData(5.0,  8.0,  2250)]
    public void AutoCalc_SpeedFromDistanceAndDuration_IsConsistentWithValidator(
        double distanceKm, double speedKmh, int durationSec)
    {
        double computed = WorkoutValidator.CalculateSpeed(distanceKm, durationSec);
        Assert.True(WorkoutValidator.IsPhysicallyConsistent(distanceKm, computed, durationSec));
    }

    // ── Division-by-zero safety ───────────────────────────────────────────────

    [Fact]
    public void NoDivisionByZero_CalculateSpeed_ZeroDuration()
        => Assert.Equal(0.0, WorkoutValidator.CalculateSpeed(10.0, 0));

    [Fact]
    public void NoDivisionByZero_IsConsistent_ZeroSpeed()
        => Assert.False(WorkoutValidator.IsPhysicallyConsistent(10.0, 0.0, 3600));

    [Fact]
    public void NoDivisionByZero_IsConsistent_ZeroDistance()
        => Assert.False(WorkoutValidator.IsPhysicallyConsistent(0.0, 10.0, 3600));
}
