using TreadLog.Models;

namespace TreadLog.Tests.Models;

public class WorkoutSessionTests
{
    // ── Default state ──────────────────────────────────────────────────────────

    [Fact]
    public void NewInstance_NullableFields_DefaultToNull()
    {
        var session = new WorkoutSession();

        Assert.Null(session.CaloriesBurned);
        Assert.Null(session.AvgHeartRate);
    }

    [Fact]
    public void NewInstance_InclinePercent_DefaultsToZero()
    {
        var session = new WorkoutSession();

        Assert.Equal(0.0, session.InclinePercent);
    }

    [Fact]
    public void NewInstance_NumericFields_DefaultToZero()
    {
        var session = new WorkoutSession();

        Assert.Equal(0, session.Id);
        Assert.Equal(0, session.DurationSeconds);
        Assert.Equal(0.0, session.DistanceKm);
        Assert.Equal(0.0, session.AvgSpeedKmh);
    }

    [Fact]
    public void NewInstance_AuditStrings_DefaultToEmpty()
    {
        var session = new WorkoutSession();

        Assert.Equal(string.Empty, session.CreatedAt);
        Assert.Equal(string.Empty, session.UpdatedAt);
    }

    // ── Property round-trips ───────────────────────────────────────────────────

    [Fact]
    public void AllMandatoryProperties_SetAndRetrievedCorrectly()
    {
        var date      = new DateTime(2026, 5, 16, 7, 30, 0, DateTimeKind.Utc);
        const string iso = "2026-05-16T07:30:00.0000000Z";

        var session = new WorkoutSession
        {
            Id              = 42,
            SessionDate     = date,
            DurationSeconds = 3600,
            DistanceKm      = 10.5,
            AvgSpeedKmh     = 10.5,
            InclinePercent  = 2.0,
            CreatedAt       = iso,
            UpdatedAt       = iso
        };

        Assert.Equal(42,    session.Id);
        Assert.Equal(date,  session.SessionDate);
        Assert.Equal(3600,  session.DurationSeconds);
        Assert.Equal(10.5,  session.DistanceKm);
        Assert.Equal(10.5,  session.AvgSpeedKmh);
        Assert.Equal(2.0,   session.InclinePercent);
        Assert.Equal(iso,   session.CreatedAt);
        Assert.Equal(iso,   session.UpdatedAt);
    }

    [Fact]
    public void OptionalProperties_SetAndRetrievedCorrectly()
    {
        var session = new WorkoutSession { CaloriesBurned = 450, AvgHeartRate = 145 };

        Assert.Equal(450, session.CaloriesBurned);
        Assert.Equal(145, session.AvgHeartRate);
    }

    [Fact]
    public void OptionalProperties_AcceptNull()
    {
        var session = new WorkoutSession { CaloriesBurned = 500, AvgHeartRate = 150 };
        session.CaloriesBurned = null;
        session.AvgHeartRate   = null;

        Assert.Null(session.CaloriesBurned);
        Assert.Null(session.AvgHeartRate);
    }

    [Fact]
    public void AuditStrings_StoreRawIso8601WithoutParsing()
    {
        const string raw = "2026-05-16T07:30:00.0000000+02:00";
        var session = new WorkoutSession { CreatedAt = raw, UpdatedAt = raw };

        // Model must return the string exactly as assigned — no parsing, no normalisation
        Assert.Equal(raw, session.CreatedAt);
        Assert.Equal(raw, session.UpdatedAt);
    }

    // ── Boundary values ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(60)]
    [InlineData(3600)]
    [InlineData(7200)]
    public void DurationSeconds_AcceptsValidValues(int seconds)
    {
        var session = new WorkoutSession { DurationSeconds = seconds };

        Assert.Equal(seconds, session.DurationSeconds);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(7.5)]
    [InlineData(15.0)]
    public void InclinePercent_AcceptsBoundaryValues(double incline)
    {
        var session = new WorkoutSession { InclinePercent = incline };

        Assert.Equal(incline, session.InclinePercent);
    }

    [Theory]
    [InlineData(40)]
    [InlineData(145)]
    [InlineData(220)]
    public void AvgHeartRate_AcceptsBoundaryValues(int bpm)
    {
        var session = new WorkoutSession { AvgHeartRate = bpm };

        Assert.Equal(bpm, session.AvgHeartRate);
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(5.5)]
    [InlineData(42.0)]
    public void DistanceKm_AcceptsPositiveValues(double km)
    {
        var session = new WorkoutSession { DistanceKm = km };

        Assert.Equal(km, session.DistanceKm);
    }
}
