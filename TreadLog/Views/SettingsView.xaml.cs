using System.Windows;
using System.Windows.Controls;
using TreadLog.ViewModels;

namespace TreadLog.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            vm.LoadCommand.Execute(null);
    }
}
