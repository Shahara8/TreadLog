using System.Globalization;
using System.Windows;
using System.Windows.Data;
using TreadLog.ViewModels.Support;

namespace TreadLog.Converters;

/// <summary>bool → Visibility (true = Visible, false = Collapsed)</summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

/// <summary>string → Visibility (non-empty = Visible, empty/null = Collapsed)</summary>
[ValueConversion(typeof(string), typeof(Visibility))]
public sealed class NotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Enum ↔ bool for RadioButton binding.
/// ConverterParameter is the enum name as a string.
/// Returns true when the bound value matches the parameter.
/// </summary>
public sealed class EnumToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.ToString() == parameter?.ToString();

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true && parameter is string name && targetType.IsEnum)
            return Enum.Parse(targetType, name);
        return DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// Enum → Visibility. Returns Visible when the bound value matches ConverterParameter.
/// Used to show/hide the custom date pickers on the Dashboard.
/// </summary>
public sealed class EnumToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.ToString() == parameter?.ToString() ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// string ↔ bool for RadioButton binding to a string property.
/// ConverterParameter is the expected string value.
/// Returns true when the bound string matches the parameter.
/// </summary>
public sealed class StringEqualConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? parameter?.ToString() ?? string.Empty : DependencyProperty.UnsetValue;
}
