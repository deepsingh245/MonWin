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

    public MetricControl()
    {
        InitializeComponent();
    }
}
