using TreadLog.Helpers;

namespace TreadLog.Tests.Helpers;

public class UnitConverterTests
{
    // ── KmToMiles ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1.0,   0.621371)]
    [InlineData(5.0,   3.106856)]
    [InlineData(10.0,  6.213712)]
    [InlineData(42.195, 26.21876)]  // marathon distance
    public void KmToMiles_KnownValues_MatchExpected(double km, double expectedMiles)
    {
        Assert.Equal(expectedMiles, UnitConverter.KmToMiles(km), 4);
    }

    [Fact]
    public void KmToMiles_Zero_ReturnsZero()
        => Assert.Equal(0.0, UnitConverter.KmToMiles(0.0));

    [Fact]
    public void KmToMiles_NegativeInput_ReturnsNegative()
        => Assert.True(UnitConverter.KmToMiles(-5.0) < 0);

    [Fact]
    public void KmToMiles_LargeValue_DoesNotOverflow()
        => Assert.True(UnitConverter.KmToMiles(1_000_000) > 0);

    // ── MilesToKm ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1.0,  1.60934)]
    [InlineData(5.0,  8.04670)]
    [InlineData(26.21876, 42.195)] // marathon distance
    public void MilesToKm_KnownValues_MatchExpected(double miles, double expectedKm)
    {
        Assert.Equal(expectedKm, UnitConverter.MilesToKm(miles), 3);
    }

    [Fact]
    public void MilesToKm_Zero_ReturnsZero()
        => Assert.Equal(0.0, UnitConverter.MilesToKm(0.0));

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1.0)]
    [InlineData(5.0)]
    [InlineData(42.195)]
    [InlineData(0.1)]
    public void KmToMiles_ThenMilesToKm_RecoverOriginalValue(double originalKm)
    {
        double roundTripped = UnitConverter.MilesToKm(UnitConverter.KmToMiles(originalKm));
        Assert.Equal(originalKm, roundTripped, 10);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(5.0)]
    [InlineData(26.2)]
    public void MilesToKm_ThenKmToMiles_RecoverOriginalValue(double originalMiles)
    {
        double roundTripped = UnitConverter.KmToMiles(UnitConverter.MilesToKm(originalMiles));
        Assert.Equal(originalMiles, roundTripped, 10);
    }

    // ── KmhToMph ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1.0,   0.621371)]
    [InlineData(10.0,  6.213712)]
    [InlineData(16.0,  9.941964)]
    public void KmhToMph_KnownValues_MatchExpected(double kmh, double expectedMph)
    {
        Assert.Equal(expectedMph, UnitConverter.KmhToMph(kmh), 4);
    }

    [Fact]
    public void KmhToMph_Zero_ReturnsZero()
        => Assert.Equal(0.0, UnitConverter.KmhToMph(0.0));

    // ── MphToKmh ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1.0,  1.60934)]
    [InlineData(6.0,  9.65604)]
    [InlineData(10.0, 16.09340)]
    public void MphToKmh_KnownValues_MatchExpected(double mph, double expectedKmh)
    {
        Assert.Equal(expectedKmh, UnitConverter.MphToKmh(mph), 4);
    }

    [Fact]
    public void KmhToMph_ThenMphToKmh_RecoverOriginalValue()
    {
        double roundTripped = UnitConverter.MphToKmh(UnitConverter.KmhToMph(10.0));
        Assert.Equal(10.0, roundTripped, 10);
    }

    // ── CalculatePace — normal cases ──────────────────────────────────────────

    [Fact]
    public void CalculatePace_TenKmInOneHour_KmUnit_ReturnsSixMinutesPerKm()
    {
        // 3600 s / 10 km = 360 s/km = exactly 6:00
        var pace = UnitConverter.CalculatePace(10.0, 3600.0, "km");

        Assert.Equal(TimeSpan.FromSeconds(360), pace);
    }

    [Fact]
    public void CalculatePace_FiveKmIn1800s_KmUnit_ReturnsSixMinutesPerKm()
    {
        var pace = UnitConverter.CalculatePace(5.0, 1800.0, "km");

        Assert.Equal(TimeSpan.FromSeconds(360), pace);
    }

    [Fact]
    public void CalculatePace_TenKmInOneHour_MiUnit_ReturnsCorrectMinutesPerMile()
    {
        // 10 km = 6.21371 mi; 3600 / 6.21371 ≈ 579.65 s/mi → 9 min 39 s
        var pace = UnitConverter.CalculatePace(10.0, 3600.0, "mi");

        Assert.Equal(9,  (int)pace.TotalMinutes);
        Assert.Equal(39, pace.Seconds);
    }

    [Fact]
    public void CalculatePace_DefaultUnit_IsTreatedAsKm()
    {
        var withDefault  = UnitConverter.CalculatePace(10.0, 3600.0);
        var withExplicit = UnitConverter.CalculatePace(10.0, 3600.0, "km");

        Assert.Equal(withExplicit, withDefault);
    }

    // ── CalculatePace — defensive edge cases ──────────────────────────────────

    [Fact]
    public void CalculatePace_ZeroDistance_ReturnsZero()
        => Assert.Equal(TimeSpan.Zero, UnitConverter.CalculatePace(0.0, 3600.0));

    [Fact]
    public void CalculatePace_ZeroDuration_ReturnsZero()
        => Assert.Equal(TimeSpan.Zero, UnitConverter.CalculatePace(10.0, 0.0));

    [Fact]
    public void CalculatePace_NegativeDistance_ReturnsZero()
        => Assert.Equal(TimeSpan.Zero, UnitConverter.CalculatePace(-10.0, 3600.0));

    [Fact]
    public void CalculatePace_NegativeDuration_ReturnsZero()
        => Assert.Equal(TimeSpan.Zero, UnitConverter.CalculatePace(10.0, -3600.0));

    [Fact]
    public void CalculatePace_NaNDistance_ReturnsZero()
        => Assert.Equal(TimeSpan.Zero, UnitConverter.CalculatePace(double.NaN, 3600.0));

    [Fact]
    public void CalculatePace_NaNDuration_ReturnsZero()
        => Assert.Equal(TimeSpan.Zero, UnitConverter.CalculatePace(10.0, double.NaN));

    [Fact]
    public void CalculatePace_InfiniteDistance_ReturnsZero()
        => Assert.Equal(TimeSpan.Zero, UnitConverter.CalculatePace(double.PositiveInfinity, 3600.0));

    [Fact]
    public void CalculatePace_InfiniteDuration_ReturnsZero()
        => Assert.Equal(TimeSpan.Zero, UnitConverter.CalculatePace(10.0, double.PositiveInfinity));

    [Fact]
    public void CalculatePace_VerySmallDistance_DoesNotThrow()
    {
        // 0.001 km in 60 s → 60000 s/km = 1000 min/km — extreme but valid input
        var ex = Record.Exception(() => UnitConverter.CalculatePace(0.001, 60.0, "km"));
        Assert.Null(ex);
    }

    [Fact]
    public void CalculatePace_VeryLargeDistance_DoesNotThrow()
    {
        var ex = Record.Exception(() => UnitConverter.CalculatePace(100_000.0, 3600.0, "km"));
        Assert.Null(ex);
    }

    // ── FormatPace ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(360,  "6:00")]   // 6:00/km — common pace
    [InlineData(365,  "6:05")]   // fractional second component
    [InlineData(600,  "10:00")]  // double-digit minutes
    [InlineData(3661, "61:01")]  // deliberately extreme — no overflow
    [InlineData(6000, "100:00")] // triple-digit minutes — must not truncate
    public void FormatPace_KnownSeconds_ReturnsExpectedString(int totalSeconds, string expected)
    {
        Assert.Equal(expected, UnitConverter.FormatPace(TimeSpan.FromSeconds(totalSeconds)));
    }

    [Fact]
    public void FormatPace_Zero_ReturnsDashes()
        => Assert.Equal("--:--", UnitConverter.FormatPace(TimeSpan.Zero));

    [Fact]
    public void FormatPace_Negative_ReturnsDashes()
        => Assert.Equal("--:--", UnitConverter.FormatPace(TimeSpan.FromSeconds(-1)));

    [Fact]
    public void FormatPace_SingleDigitSeconds_ZeroPadsSeconds()
    {
        // 6 min 5 sec → "6:05" not "6:5"
        Assert.Equal("6:05", UnitConverter.FormatPace(TimeSpan.FromSeconds(365)));
    }

    // ── CalculatePaceString ───────────────────────────────────────────────────

    [Fact]
    public void CalculatePaceString_NormalInput_ReturnsPaceString()
        => Assert.Equal("6:00", UnitConverter.CalculatePaceString(10.0, 3600.0, "km"));

    [Fact]
    public void CalculatePaceString_ZeroDistance_ReturnsDashes()
        => Assert.Equal("--:--", UnitConverter.CalculatePaceString(0.0, 3600.0, "km"));

    [Fact]
    public void CalculatePaceString_ZeroDuration_ReturnsDashes()
        => Assert.Equal("--:--", UnitConverter.CalculatePaceString(10.0, 0.0, "km"));
}
