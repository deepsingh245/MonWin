using System.Globalization;
using System.Windows.Data;
using SystemMonitor.Models;

namespace SystemMonitor.Converters;

/// <summary>
/// MultiBinding: [DisplayMode, per-metric graph flag] → true only when the overlay is in
/// Compact+Graph mode AND that metric's sparkline hasn't been switched off.
/// </summary>
public sealed class CompactGraphVisibleConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values.Length == 2 && values[0] is DisplayMode.CompactGraph && values[1] is true;

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
