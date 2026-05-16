namespace TreadLog.ViewModels.Support;

/// <summary>Minimal data transfer object for chart series binding. No WPF dependency.</summary>
public sealed record ChartDataPoint(string Label, double Value);
