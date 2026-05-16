using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using TreadLog.ViewModels;

namespace TreadLog.Views;

public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded             += OnLoaded;
    }

    private void OnDataContextChanged(object sender,
        System.Windows.DependencyPropertyChangedEventArgs e)
    {
        // Reload data whenever this view is activated (DataContext re-set by ContentControl)
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is HistoryViewModel vm)
            vm.LoadDataCommand.Execute(null);
    }

    private void OnExportCsvClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HistoryViewModel vm) return;

        var dialog = new SaveFileDialog
        {
            Title      = "Export to CSV",
            Filter     = "CSV files (*.csv)|*.csv",
            DefaultExt = ".csv",
            FileName   = $"TreadLog_Export_{DateTime.Today:yyyy-MM-dd}.csv"
        };

        if (dialog.ShowDialog() == true)
            vm.ExportCsvCommand.Execute(dialog.FileName);
    }

    private void OnImportClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HistoryViewModel vm) return;

        var dialog = new OpenFileDialog
        {
            Title  = "Import Workout Data",
            Filter = "Supported files (*.csv;*.json)|*.csv;*.json|CSV|*.csv|JSON|*.json"
        };

        if (dialog.ShowDialog() == true)
            vm.ImportCommand.Execute(dialog.FileName);
    }
}
