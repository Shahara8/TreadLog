using TreadLog.Models;

namespace TreadLog.Tests.Models;

public class UserSettingsTests
{
    // ── Default state ──────────────────────────────────────────────────────────

    [Fact]
    public void NewInstance_DistanceUnit_DefaultsToKm()
    {
        var settings = new UserSettings();

        Assert.Equal("km", settings.DistanceUnit);
    }

    [Fact]
    public void NewInstance_Id_DefaultsToOne()
    {
        var settings = new UserSettings();

        Assert.Equal(1, settings.Id);
    }

    [Fact]
    public void NewInstance_UpdatedAt_DefaultsToEmpty()
    {
        var settings = new UserSettings();

        Assert.Equal(string.Empty, settings.UpdatedAt);
    }

    // ── Property round-trips ───────────────────────────────────────────────────

    [Theory]
    [InlineData("km")]
    [InlineData("mi")]
    public void DistanceUnit_AcceptsBothSupportedValues(string unit)
    {
        var settings = new UserSettings { DistanceUnit = unit };

        Assert.Equal(unit, settings.DistanceUnit);
    }

    [Fact]
    public void AllProperties_SetAndRetrievedCorrectly()
    {
        const string iso = "2026-05-16T12:00:00.0000000Z";

        var settings = new UserSettings
        {
            Id           = 1,
            DistanceUnit = "mi",
            UpdatedAt    = iso
        };

        Assert.Equal(1,    settings.Id);
        Assert.Equal("mi", settings.DistanceUnit);
        Assert.Equal(iso,  settings.UpdatedAt);
    }

    [Fact]
    public void UpdatedAt_StoresRawIso8601WithoutParsing()
    {
        const string raw = "2026-05-16T12:00:00.0000000+03:00";
        var settings = new UserSettings { UpdatedAt = raw };

        Assert.Equal(raw, settings.UpdatedAt);
    }

    [Fact]
    public void DistanceUnit_CanBeUpdatedFromKmToMi()
    {
        var settings = new UserSettings();
        settings.DistanceUnit = "mi";

        Assert.Equal("mi", settings.DistanceUnit);
    }
}
