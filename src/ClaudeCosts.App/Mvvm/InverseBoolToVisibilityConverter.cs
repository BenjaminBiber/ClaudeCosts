using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ClaudeCosts.App.Mvvm;

/// <summary>
/// One-way converter: <c>true</c> → <see cref="Visibility.Collapsed"/>, <c>false</c> → <see cref="Visibility.Visible"/>.
/// The inverse of the built-in <see cref="BooleanToVisibilityConverter"/>.
/// </summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Collapsed;
}
