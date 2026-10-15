using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace MonWin.Linux.Views;

/// <summary>
/// Binds an enum property to RadioButtons: IsChecked is true when the value equals the
/// parameter (an enum member name); checking the button writes that member back.
/// </summary>
public sealed class EnumMatchConverter : IValueConverter
{
    public static readonly EnumMatchConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Enum e && parameter is string name && string.Equals(e.ToString(), name, StringComparison.Ordinal);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && parameter is string name && Enum.TryParse(targetType, name, out var parsed))
        {
            return parsed;
        }

        return BindingOperations.DoNothing;
    }
}
