using System.Windows;
using System.Windows.Controls;

namespace SystemMonitor.Controls;

/// <summary>
/// Composite label + value + sparkline used for both the tiny overlay (Compact /
/// Compact+Graph, via <see cref="IsCompact"/>=true) and the larger Detailed
/// presentation (IsCompact=false) — one control, two layouts, so the bound data never
/// needs duplicating between them.
/// </summary>
public partial class MetricControl : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(MetricControl), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueTextProperty = DependencyProperty.Register(
        nameof(ValueText), typeof(string), typeof(MetricControl), new PropertyMetadata("N/A"));

    public static readonly DependencyProperty SecondaryTextProperty = DependencyProperty.Register(
        nameof(SecondaryText), typeof(string), typeof(MetricControl), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(double[]), typeof(MetricControl), new PropertyMetadata(Array.Empty<double>()));

    public static readonly DependencyProperty IsCompactProperty = DependencyProperty.Register(
        nameof(IsCompact), typeof(bool), typeof(MetricControl), new PropertyMetadata(true));

    public static readonly DependencyProperty ShowGraphProperty = DependencyProperty.Register(
        nameof(ShowGraph), typeof(bool), typeof(MetricControl), new PropertyMetadata(true));

    /// <summary>Sparkline scale ceiling. 100 for percentages; 0 auto-scales to the history's own max (for byte rates).</summary>
    public static readonly DependencyProperty GraphMaxValueProperty = DependencyProperty.Register(
        nameof(GraphMaxValue), typeof(double), typeof(MetricControl), new PropertyMetadata(100.0));

    /// <summary>Minimum width of the compact value text, so wider values (e.g. network rates) don't make the overlay jitter.</summary>
    public static readonly DependencyProperty ValueMinWidthProperty = DependencyProperty.Register(
        nameof(ValueMinWidth), typeof(double), typeof(MetricControl), new PropertyMetadata(30.0));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string ValueText
    {
        get => (string)GetValue(ValueTextProperty);
        set => SetValue(ValueTextProperty, value);
    }

    public string SecondaryText
    {
        get => (string)GetValue(SecondaryTextProperty);
        set => SetValue(SecondaryTextProperty, value);
    }

    public double[] Values
    {
        get => (double[])GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    public bool ShowGraph
    {
        get => (bool)GetValue(ShowGraphProperty);
        set => SetValue(ShowGraphProperty, value);
    }

    public double GraphMaxValue
    {
        get => (double)GetValue(GraphMaxValueProperty);
        set => SetValue(GraphMaxValueProperty, value);
    }

    public double ValueMinWidth
    {
        get => (double)GetValue(ValueMinWidthProperty);
        set => SetValue(ValueMinWidthProperty, value);
    }

    public MetricControl()
    {
        InitializeComponent();
    }
}
