using System.Text;
using System.Text.Json;
using Moq;
using TreadLog.Models;
using TreadLog.Services;
using TreadLog.Services.Interfaces;
using Xunit;

namespace TreadLog.Tests.Services;

/// <summary>
/// All tests write to real temporary files and clean up after themselves.
/// The repository is mocked so no database is touched.
/// </summary>
public class DataPortabilityServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IWorkoutRepository> _repoMock;
    private readonly DataPortabilityService _svc;

    public DataPortabilityServiceTests()
    {
        _tempDir  = Path.Combine(Path.GetTempPath(), $"TreadLogTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _repoMock = new Mock<IWorkoutRepository>();
        _repoMock.Setup(r => r.BulkInsertIgnoreAsync(It.IsAny<IEnumerable<WorkoutSession>>()))
                 .ReturnsAsync((IEnumerable<WorkoutSession> s) => s.Count());

        _svc = new DataPortabilityService(_repoMock.Object);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string TempFile(string name) => Path.Combine(_tempDir, name);

    private static WorkoutSession MakeSession(
        int id = 1, double distKm = 5.0, double speedKmh = 10.0,
        int durSec = 1800, double incline = 1.5,
        DateTime? date = null,
        int? calories = 350, int? hr = 145) =>
        new()
        {
            Id              = id,
            SessionDate     = (date ?? new DateTime(2024, 3, 15, 8, 0, 0, DateTimeKind.Utc)),
            DurationSeconds = durSec,
            DistanceKm      = distKm,
            AvgSpeedKmh     = speedKmh,
            InclinePercent  = incline,
            CaloriesBurned  = calories,
            AvgHeartRate    = hr,
            CreatedAt       = "2024-03-15T08:00:00.000Z",
            UpdatedAt       = "2024-03-15T08:00:00.000Z"
        };

    // ── CSV export ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExportToCsvAsync_CreatesFile()
    {
        string path = TempFile("export.csv");
        await _svc.ExportToCsvAsync(new[] { MakeSession() }, path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task ExportToCsvAsync_WritesHeaderRow()
    {
        string path = TempFile("export.csv");
        await _svc.ExportToCsvAsync(new[] { MakeSession() }, path);
        string[] lines = await File.ReadAllLinesAsync(path);
        Assert.StartsWith("Id,", lines[0]);
        Assert.Contains("SessionDate", lines[0]);
        Assert.Contains("DistanceKm", lines[0]);
    }

    [Fact]
    public async Task ExportToCsvAsync_WritesOneRowPerSession()
    {
        string path = TempFile("export.csv");
        await _svc.ExportToCsvAsync(new[] { MakeSession(1), MakeSession(2) }, path);
        string[] lines = await File.ReadAllLinesAsync(path);
        // 1 header + 2 data rows
        Assert.Equal(3, lines.Length);
    }

    [Fact]
    public async Task ExportToCsvAsync_EmptyList_WritesHeaderOnly()
    {
        string path = TempFile("empty.csv");
        await _svc.ExportToCsvAsync(Enumerable.Empty<WorkoutSession>(), path);
        string[] lines = await File.ReadAllLinesAsync(path);
        Assert.Single(lines);
    }

    [Fact]
    public async Task ExportToCsvAsync_IncludesDistanceKm()
    {
        string path = TempFile("export.csv");
        var session = MakeSession(distKm: 7.42);
        await _svc.ExportToCsvAsync(new[] { session }, path);
        string content = await File.ReadAllTextAsync(path);
        Assert.Contains("7.42", content);
    }

    // ── JSON export ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ExportToJsonAsync_CreatesValidJson()
    {
        string path = TempFile("export.json");
        await _svc.ExportToJsonAsync(new[] { MakeSession() }, path);
        string json = await File.ReadAllTextAsync(path);
        var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task ExportToJsonAsync_WritesCorrectSessionCount()
    {
        string path = TempFile("export.json");
        await _svc.ExportToJsonAsync(new[] { MakeSession(1), MakeSession(2) }, path);
        string json = await File.ReadAllTextAsync(path);
        var doc  = JsonDocument.Parse(json);
        Assert.Equal(2, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task ExportToJsonAsync_ContainsDistanceKm()
    {
        string path = TempFile("export.json");
        await _svc.ExportToJsonAsync(new[] { MakeSession(distKm: 8.5) }, path);
        string json = await File.ReadAllTextAsync(path);
        Assert.Contains("8.5", json);
    }

    [Fact]
    public async Task ExportToJsonAsync_OmitsNullOptionalFields()
    {
        string path = TempFile("export_nulls.json");
        var session = MakeSession(calories: null, hr: null);
        await _svc.ExportToJsonAsync(new[] { session }, path);
        string json = await File.ReadAllTextAsync(path);
        // WhenWritingNull means CaloriesBurned and AvgHeartRate should be absent
        Assert.DoesNotContain("CaloriesBurned", json);
        Assert.DoesNotContain("AvgHeartRate", json);
    }

    // ── CSV import ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportFromFileAsync_Csv_InsertsValidSessions()
    {
        // Round-trip: export then import
        string path = TempFile("roundtrip.csv");
        await _svc.ExportToCsvAsync(new[] { MakeSession() }, path);

        var result = await _svc.ImportFromFileAsync(path);

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.InsertedRows);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ImportFromFileAsync_Csv_ReportsInsertedCount()
    {
        string path = TempFile("multi.csv");
        await _svc.ExportToCsvAsync(new[] { MakeSession(1), MakeSession(2) }, path);

        var result = await _svc.ImportFromFileAsync(path);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.InsertedRows);
    }

    [Fact]
    public async Task ImportFromFileAsync_Csv_BadRow_ReportsError()
    {
        string path = TempFile("bad.csv");
        // Write a CSV with one valid and one malformed row
        await File.WriteAllTextAsync(path,
            "Id,SessionDate,DurationSeconds,DistanceKm,AvgSpeedKmh,InclinePercent,CaloriesBurned,AvgHeartRate,CreatedAt,UpdatedAt\n" +
            "1,2024-03-15T08:00:00.0000000Z,1800,5.0,10.0,1.5,350,145,,\n" +
            "2,NOT-A-DATE,1800,5.0,10.0,1.5,,,," +
            "\n");
        _repoMock.Setup(r => r.BulkInsertIgnoreAsync(It.IsAny<IEnumerable<WorkoutSession>>()))
                 .ReturnsAsync(1);

        var result = await _svc.ImportFromFileAsync(path);

        Assert.Equal(1, result.InsertedRows);
        Assert.Single(result.Errors);
    }

    [Fact]
    public async Task ImportFromFileAsync_Csv_MissingHeader_ReturnsError()
    {
        string path = TempFile("missingcol.csv");
        // Header missing DistanceKm
        await File.WriteAllTextAsync(path,
            "Id,SessionDate,DurationSeconds,AvgSpeedKmh,InclinePercent\n" +
            "1,2024-03-15T08:00:00.0000000Z,1800,10.0,1.5\n");

        var result = await _svc.ImportFromFileAsync(path);

        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task ImportFromFileAsync_Csv_EmptyFile_ReturnsZeroRows()
    {
        string path = TempFile("empty.csv");
        await File.WriteAllTextAsync(path, "");

        var result = await _svc.ImportFromFileAsync(path);

        Assert.Equal(0, result.TotalRows);
        Assert.Equal(0, result.InsertedRows);
    }

    // ── JSON import ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportFromFileAsync_Json_InsertsValidSessions()
    {
        string path = TempFile("roundtrip.json");
        await _svc.ExportToJsonAsync(new[] { MakeSession() }, path);

        var result = await _svc.ImportFromFileAsync(path);

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.InsertedRows);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ImportFromFileAsync_Json_InvalidJson_ReturnsError()
    {
        string path = TempFile("invalid.json");
        await File.WriteAllTextAsync(path, "{ this is not valid JSON }");

        var result = await _svc.ImportFromFileAsync(path);

        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task ImportFromFileAsync_Json_BadRow_ReportsError()
    {
        string path = TempFile("badrow.json");
        await File.WriteAllTextAsync(path, """
            [
              {"SessionDate": "2024-03-15T08:00:00Z", "DurationSeconds": 1800, "DistanceKm": 5.0, "AvgSpeedKmh": 10.0, "InclinePercent": 1.5},
              {"SessionDate": "NOT-A-DATE", "DurationSeconds": 1800, "DistanceKm": 5.0, "AvgSpeedKmh": 10.0, "InclinePercent": 1.5}
            ]
            """);
        _repoMock.Setup(r => r.BulkInsertIgnoreAsync(It.IsAny<IEnumerable<WorkoutSession>>()))
                 .ReturnsAsync(1);

        var result = await _svc.ImportFromFileAsync(path);

        Assert.Equal(1, result.InsertedRows);
        Assert.Single(result.Errors);
    }

    // ── Unsupported extension ─────────────────────────────────────────────────

    [Fact]
    public async Task ImportFromFileAsync_UnsupportedExtension_ReturnsError()
    {
        string path = TempFile("data.xlsx");
        var result = await _svc.ImportFromFileAsync(path);
        Assert.NotEmpty(result.Errors);
    }

    // ── Constructor guard ─────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullRepo_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DataPortabilityService(null!));
    }
}
