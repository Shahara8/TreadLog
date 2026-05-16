namespace TreadLog.Services.Interfaces;

/// <summary>Summary returned to the UI after an import operation.</summary>
public sealed record ImportResult(
    int TotalRows,
    int InsertedRows,
    int SkippedRows,
    IReadOnlyList<string> Errors);
