using System.Globalization;
using System.Windows.Data;

namespace SystemMonitor.Converters;

/// <summary>Formats a nullable numeric value, or "N/A" when null — used for optional
/// metrics like CPU frequency and GPU memory that may be unavailable on some systems.</summary>
public sealed class NullableToUnavailableTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return "N/A";
        }

        var format = parameter as string ?? "{0}";
        return string.Format(culture, format, value);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
