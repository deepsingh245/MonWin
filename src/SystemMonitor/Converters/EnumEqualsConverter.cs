using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SystemMonitor.Converters;

/// <summary>
/// Binds an enum property to a set of RadioButtons/ToggleButtons: IsChecked = (value ==
/// parameter), and setting IsChecked=true back-assigns the parameter's enum value. Also
/// works as a Visibility binding (matches → Visible) for enum-gated sections.
/// </summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isMatch = value is not null && parameter is not null && value.Equals(ParseIfNeeded(value, parameter));
        return targetType == typeof(Visibility)
            ? (isMatch ? Visibility.Visible : Visibility.Collapsed)
            : isMatch;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is not null ? ParseIfNeeded(targetType, parameter) : Binding.DoNothing;

    private static object? ParseIfNeeded(object referenceValue, object parameter)
    {
        if (parameter is string s && referenceValue is Enum)
        {
            return Enum.Parse(referenceValue.GetType(), s);
        }

        return parameter;
    }

    private static object? ParseIfNeeded(Type targetType, object parameter) =>
        parameter is string s ? Enum.Parse(targetType, s) : parameter;
}
