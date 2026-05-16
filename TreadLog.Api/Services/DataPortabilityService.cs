using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TreadLog.Models;
using TreadLog.Services.Interfaces;

namespace TreadLog.Api.Services;

/// <summary>
/// Stream-based CSV/JSON export and import for the API layer.
/// Export writes to any writable Stream (response body, MemoryStream, file).
/// Import reads from any readable Stream (request body, MemoryStream).
/// Deduplication is handled by BulkInsertIgnoreAsync (INSERT OR IGNORE on SessionDate UNIQUE INDEX).
/// </summary>
public class DataPortabilityService
{
    private readonly IWorkoutRepository _repo;

    private static readonly string[] CsvHeaders =
    {
        "Id", "SessionDate", "DurationSeconds", "DistanceKm",
        "AvgSpeedKmh", "InclinePercent", "CaloriesBurned", "AvgHeartRate",
        "CreatedAt", "UpdatedAt"
    };

    private static readonly string[] RequiredCsvColumns =
    {
        "SessionDate", "DurationSeconds", "DistanceKm", "AvgSpeedKmh", "InclinePercent"
    };

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented          = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public DataPortabilityService(IWorkoutRepository repo)
        => _repo = repo ?? throw new ArgumentNullException(nameof(repo));

    // ── Export ────────────────────────────────────────────────────────────────

    public async Task ExportToCsvAsync(IEnumerable<WorkoutSession> sessions, Stream destination)
    {
        await using var writer = new StreamWriter(destination, Encoding.UTF8, leaveOpen: true);
        await writer.WriteLineAsync(string.Join(",", CsvHeaders));
        foreach (var s in sessions)
            await writer.WriteLineAsync(ToCsvRow(s));
    }

    public async Task ExportToJsonAsync(IEnumerable<WorkoutSession> sessions, Stream destination)
    {
        var dtos = sessions.Select(ToDto).ToList();
        await JsonSerializer.SerializeAsync(destination, dtos, JsonOpts);
    }

    // ── Import ────────────────────────────────────────────────────────────────

    public async Task<ImportResult> ImportFromStreamAsync(Stream source, string fileExtension)
    {
        return fileExtension.ToLowerInvariant() switch
        {
            ".csv"  => await ImportCsvAsync(source),
            ".json" => await ImportJsonAsync(source),
            var ext => new ImportResult(0, 0, 0,
                           new[] { $"Unsupported file format '{ext}'. Use .csv or .json." })
        };
    }

    // ── CSV helpers ───────────────────────────────────────────────────────────

    private static string ToCsvRow(WorkoutSession s) =>
        string.Join(",",
            s.Id,
            QuoteCsv(s.SessionDate.ToUniversalTime().ToString("o")),
            s.DurationSeconds,
            s.DistanceKm.ToString("G", CultureInfo.InvariantCulture),
            s.AvgSpeedKmh.ToString("G", CultureInfo.InvariantCulture),
            s.InclinePercent.ToString("G", CultureInfo.InvariantCulture),
            s.CaloriesBurned?.ToString() ?? "",
            s.AvgHeartRate?.ToString() ?? "",
            QuoteCsv(s.CreatedAt),
            QuoteCsv(s.UpdatedAt));

    private static string QuoteCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private async Task<ImportResult> ImportCsvAsync(Stream source)
    {
        string[] lines;
        try
        {
            using var reader = new StreamReader(source, Encoding.UTF8, leaveOpen: true);
            var text = await reader.ReadToEndAsync();
            lines = text.Split('\n', StringSplitOptions.None)
                        .Select(l => l.TrimEnd('\r'))
                        .ToArray();
        }
        catch (Exception ex)
        {
            return new ImportResult(0, 0, 0, new[] { $"Cannot read stream: {ex.Message}" });
        }

        if (lines.Length < 2)
            return new ImportResult(0, 0, 0, Array.Empty<string>());

        var headerCols = SplitCsvLine(lines[0]).ToArray();
        var missing    = RequiredCsvColumns.Except(headerCols, StringComparer.OrdinalIgnoreCase).ToList();
        if (missing.Count > 0)
            return new ImportResult(0, 0, 0,
                new[] { $"CSV header missing required columns: {string.Join(", ", missing)}" });

        var colIndex = headerCols
            .Select((h, i) => (h, i))
            .ToDictionary(t => t.h, t => t.i, StringComparer.OrdinalIgnoreCase);

        var sessions  = new List<WorkoutSession>();
        var errors    = new List<string>();
        int totalRows = lines.Length - 1;

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) { totalRows--; continue; }

            var (session, error) = TryParseCsvRow(lines[i], i + 1, colIndex);
            if (session != null) sessions.Add(session);
            else if (error != null) errors.Add(error);
        }

        int inserted = sessions.Count > 0 ? await _repo.BulkInsertIgnoreAsync(sessions) : 0;
        int skipped  = Math.Max(0, totalRows - inserted - errors.Count);
        return new ImportResult(totalRows, inserted, skipped, errors);
    }

    private static (WorkoutSession? session, string? error) TryParseCsvRow(
        string line, int lineNum, Dictionary<string, int> colIndex)
    {
        try
        {
            var cols = SplitCsvLine(line).ToArray();

            string Get(string name) =>
                colIndex.TryGetValue(name, out int idx) && idx < cols.Length ? cols[idx].Trim() : "";

            if (!DateTime.TryParse(Get("SessionDate"),
                    null, DateTimeStyles.RoundtripKind, out var date))
                return (null, $"Row {lineNum}: invalid SessionDate '{Get("SessionDate")}'");

            if (!int.TryParse(Get("DurationSeconds"), out int dur) || dur <= 0)
                return (null, $"Row {lineNum}: invalid DurationSeconds '{Get("DurationSeconds")}'");

            if (!double.TryParse(Get("DistanceKm"), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double dist) || dist <= 0)
                return (null, $"Row {lineNum}: invalid DistanceKm '{Get("DistanceKm")}'");

            if (!double.TryParse(Get("AvgSpeedKmh"), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double speed) || speed <= 0)
                return (null, $"Row {lineNum}: invalid AvgSpeedKmh '{Get("AvgSpeedKmh")}'");

            if (!double.TryParse(Get("InclinePercent"), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double incline))
                return (null, $"Row {lineNum}: invalid InclinePercent '{Get("InclinePercent")}'");

            int? calories  = null;
            int? heartRate = null;
            string calStr  = Get("CaloriesBurned");
            string hrStr   = Get("AvgHeartRate");
            if (!string.IsNullOrEmpty(calStr) && int.TryParse(calStr, out int cal)) calories  = cal;
            if (!string.IsNullOrEmpty(hrStr)  && int.TryParse(hrStr,  out int hr))  heartRate = hr;

            return (new WorkoutSession
            {
                SessionDate     = date,
                DurationSeconds = dur,
                DistanceKm      = dist,
                AvgSpeedKmh     = speed,
                InclinePercent  = incline,
                CaloriesBurned  = calories,
                AvgHeartRate    = heartRate
            }, null);
        }
        catch (Exception ex)
        {
            return (null, $"Row {lineNum}: {ex.Message}");
        }
    }

    private static IEnumerable<string> SplitCsvLine(string line)
    {
        var sb       = new StringBuilder();
        bool inQuote = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuote)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                { sb.Append('"'); i++; }
                else if (c == '"') inQuote = false;
                else sb.Append(c);
            }
            else
            {
                if (c == '"') inQuote = true;
                else if (c == ',') { yield return sb.ToString(); sb.Clear(); }
                else sb.Append(c);
            }
        }
        yield return sb.ToString();
    }

    // ── JSON helpers ──────────────────────────────────────────────────────────

    private async Task<ImportResult> ImportJsonAsync(Stream source)
    {
        List<SessionExportDto>? dtos;
        try
        {
            dtos = await JsonSerializer.DeserializeAsync<List<SessionExportDto>>(source, JsonOpts);
        }
        catch (Exception ex)
        {
            return new ImportResult(0, 0, 0, new[] { $"Invalid JSON: {ex.Message}" });
        }

        if (dtos == null || dtos.Count == 0)
            return new ImportResult(0, 0, 0, Array.Empty<string>());

        var sessions = new List<WorkoutSession>();
        var errors   = new List<string>();
        int rowNum   = 0;

        foreach (var dto in dtos)
        {
            rowNum++;
            var (session, error) = TryConvertDto(dto, rowNum);
            if (session != null) sessions.Add(session);
            else if (error != null) errors.Add(error);
        }

        int total    = dtos.Count;
        int inserted = sessions.Count > 0 ? await _repo.BulkInsertIgnoreAsync(sessions) : 0;
        int skipped  = Math.Max(0, total - inserted - errors.Count);
        return new ImportResult(total, inserted, skipped, errors);
    }

    private static (WorkoutSession? session, string? error) TryConvertDto(
        SessionExportDto dto, int rowNum)
    {
        if (dto.SessionDate == null)
            return (null, $"Row {rowNum}: SessionDate is required");

        if (!DateTime.TryParse(dto.SessionDate,
                null, DateTimeStyles.RoundtripKind, out var date))
            return (null, $"Row {rowNum}: invalid SessionDate '{dto.SessionDate}'");

        if (dto.DurationSeconds <= 0)
            return (null, $"Row {rowNum}: DurationSeconds must be > 0");

        if (dto.DistanceKm <= 0)
            return (null, $"Row {rowNum}: DistanceKm must be > 0");

        if (dto.AvgSpeedKmh <= 0)
            return (null, $"Row {rowNum}: AvgSpeedKmh must be > 0");

        return (new WorkoutSession
        {
            SessionDate     = date,
            DurationSeconds = dto.DurationSeconds,
            DistanceKm      = dto.DistanceKm,
            AvgSpeedKmh     = dto.AvgSpeedKmh,
            InclinePercent  = dto.InclinePercent,
            CaloriesBurned  = dto.CaloriesBurned,
            AvgHeartRate    = dto.AvgHeartRate
        }, null);
    }

    private static SessionExportDto ToDto(WorkoutSession s) => new(
        s.SessionDate.ToUniversalTime().ToString("o"),
        s.DurationSeconds, s.DistanceKm, s.AvgSpeedKmh, s.InclinePercent,
        s.CaloriesBurned, s.AvgHeartRate);
}

internal sealed record SessionExportDto(
    string? SessionDate,
    int     DurationSeconds,
    double  DistanceKm,
    double  AvgSpeedKmh,
    double  InclinePercent,
    int?    CaloriesBurned,
    int?    AvgHeartRate);
