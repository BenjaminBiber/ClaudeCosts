using System.Globalization;
using System.Windows.Data;

namespace ClaudeCosts.App.Mvvm;

/// <summary>
/// One-way converter: returns <c>true</c> when the bound enum value's name equals
/// the <c>ConverterParameter</c> string. Used to check granularity toggles/menu items.
/// </summary>
public sealed class EnumMatchToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is not null &&
        string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
