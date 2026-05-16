using System.Collections.Specialized;
using System.Windows.Controls;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using TreadLog.ViewModels;

namespace TreadLog.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded             += (_, _) => TryLoadAndBuildCharts();
    }

    private void OnDataContextChanged(object sender,
        System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is DashboardViewModel old)
        {
            old.WeeklyDistancePoints.CollectionChanged -= OnChartDataChanged;
            old.SpeedTrendPoints.CollectionChanged     -= OnChartDataChanged;
            old.InclineTrendPoints.CollectionChanged   -= OnChartDataChanged;
        }

        if (e.NewValue is DashboardViewModel vm)
        {
            vm.WeeklyDistancePoints.CollectionChanged += OnChartDataChanged;
            vm.SpeedTrendPoints.CollectionChanged     += OnChartDataChanged;
            vm.InclineTrendPoints.CollectionChanged   += OnChartDataChanged;
            BuildCharts(vm);
        }
    }

    private void OnChartDataChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => TryLoadAndBuildCharts();

    private void TryLoadAndBuildCharts()
    {
        if (DataContext is DashboardViewModel vm)
            BuildCharts(vm);
    }

    private void BuildCharts(DashboardViewModel vm)
    {
        BuildWeeklyChart(vm);
        BuildTrendChart(vm);
    }

    // ── Weekly distance bar chart ─────────────────────────────────────────────

    private void BuildWeeklyChart(DashboardViewModel vm)
    {
        var model = new PlotModel
        {
            Background     = OxyColors.Transparent,
            PlotAreaBackground = OxyColors.Transparent,
            TextColor      = OxyColor.Parse("#94A3B8"),
            PlotAreaBorderColor = OxyColors.Transparent
        };

        if (vm.WeeklyDistancePoints.Count == 0)
        {
            WeeklyPlot.Model = model;
            return;
        }

        var series = new BarSeries
        {
            FillColor    = OxyColor.Parse("#7C3AED"),
            TrackerFormatString = "{0}\n{2}: {4:0.00}"
        };

        var catAxis = new CategoryAxis
        {
            Position  = AxisPosition.Left,
            TextColor = OxyColor.Parse("#94A3B8"),
            TickStyle = TickStyle.None
        };

        foreach (var pt in vm.WeeklyDistancePoints)
        {
            series.Items.Add(new BarItem(pt.Value));
            catAxis.Labels.Add(pt.Label);
        }

        var valueAxis = new LinearAxis
        {
            Position      = AxisPosition.Bottom,
            Minimum       = 0,
            TextColor     = OxyColor.Parse("#94A3B8"),
            TicklineColor = OxyColor.Parse("#334155"),
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColor.Parse("#1E3045"),
            TickStyle     = TickStyle.None
        };

        model.Axes.Add(catAxis);
        model.Axes.Add(valueAxis);
        model.Series.Add(series);
        WeeklyPlot.Model = model;
    }

    // ── Speed + incline trend line chart ──────────────────────────────────────

    private void BuildTrendChart(DashboardViewModel vm)
    {
        var model = new PlotModel
        {
            Background          = OxyColors.Transparent,
            PlotAreaBackground  = OxyColors.Transparent,
            TextColor           = OxyColor.Parse("#94A3B8"),
            PlotAreaBorderColor = OxyColors.Transparent
        };
        model.Legends.Add(new OxyPlot.Legends.Legend
        {
            LegendBackground   = OxyColors.Transparent,
            LegendBorderThickness = 0
        });

        if (vm.SpeedTrendPoints.Count == 0)
        {
            TrendPlot.Model = model;
            return;
        }

        var speedSeries = new LineSeries
        {
            Title            = "Speed",
            Color            = OxyColor.Parse("#38BDF8"),
            StrokeThickness  = 2,
            MarkerType       = MarkerType.Circle,
            MarkerSize       = 4,
            MarkerFill       = OxyColor.Parse("#38BDF8")
        };

        var inclineSeries = new LineSeries
        {
            Title            = "Incline %",
            Color            = OxyColor.Parse("#F97316"),
            StrokeThickness  = 2,
            MarkerType       = MarkerType.Square,
            MarkerSize       = 4,
            MarkerFill       = OxyColor.Parse("#F97316")
        };

        for (int i = 0; i < vm.SpeedTrendPoints.Count; i++)
        {
            speedSeries.Points.Add(new DataPoint(i, vm.SpeedTrendPoints[i].Value));
            if (i < vm.InclineTrendPoints.Count)
                inclineSeries.Points.Add(new DataPoint(i, vm.InclineTrendPoints[i].Value));
        }

        var xAxis = new LinearAxis
        {
            Position      = AxisPosition.Bottom,
            Minimum       = -0.5,
            Maximum       = vm.SpeedTrendPoints.Count - 0.5,
            IsAxisVisible = false
        };

        var yAxis = new LinearAxis
        {
            Position      = AxisPosition.Left,
            Minimum       = 0,
            TextColor     = OxyColor.Parse("#94A3B8"),
            TicklineColor = OxyColor.Parse("#334155"),
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColor.Parse("#1E3045"),
            TickStyle     = TickStyle.None
        };

        model.Axes.Add(xAxis);
        model.Axes.Add(yAxis);
        model.Series.Add(speedSeries);
        model.Series.Add(inclineSeries);
        TrendPlot.Model = model;
    }
}
