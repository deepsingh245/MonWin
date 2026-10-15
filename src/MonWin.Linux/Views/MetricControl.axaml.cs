using Avalonia;
using Avalonia.Controls;
using SystemMonitor.Models;

namespace MonWin.Linux.Views;

/// <summary>
/// Label + value + sparkline used for both the tiny overlay row (IsCompact = true) and the
/// larger detailed block (IsCompact = false): one control, two layouts, so bound data is
/// never duplicated between them.
/// </summary>
public partial class MetricControl : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<MetricControl, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<string> ValueTextProperty =
        AvaloniaProperty.Register<MetricControl, string>(nameof(ValueText), "N/A");

    public static readonly StyledProperty<string> SecondaryTextProperty =
        AvaloniaProperty.Register<MetricControl, string>(nameof(SecondaryText), string.Empty);

    public static readonly StyledProperty<double[]?> ValuesProperty =
        AvaloniaProperty.Register<MetricControl, double[]?>(nameof(Values));

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<MetricControl, bool>(nameof(IsCompact), true);

    public static readonly StyledProperty<bool> ShowGraphProperty =
        AvaloniaProperty.Register<MetricControl, bool>(nameof(ShowGraph), true);

    public static readonly StyledProperty<double> GraphMaxValueProperty =
        AvaloniaProperty.Register<MetricControl, double>(nameof(GraphMaxValue), 100.0);

    public static readonly StyledProperty<double> ValueMinWidthProperty =
        AvaloniaProperty.Register<MetricControl, double>(nameof(ValueMinWidth), 30.0);

    public static readonly StyledProperty<MetricSeverity> SeverityProperty =
        AvaloniaProperty.Register<MetricControl, MetricSeverity>(nameof(Severity));

    public static readonly DirectProperty<MetricControl, bool> HasSecondaryTextProperty =
        AvaloniaProperty.RegisterDirect<MetricControl, bool>(nameof(HasSecondaryText), c => c.HasSecondaryText);

    public MetricControl()
    {
        InitializeComponent();
    }

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public string ValueText { get => GetValue(ValueTextProperty); set => SetValue(ValueTextProperty, value); }

    public string SecondaryText { get => GetValue(SecondaryTextProperty); set => SetValue(SecondaryTextProperty, value); }

    public double[]? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    public bool ShowGraph { get => GetValue(ShowGraphProperty); set => SetValue(ShowGraphProperty, value); }

    /// <summary>Sparkline scale ceiling. 100 for percentages; 0 auto-scales to the history's own maximum (byte rates).</summary>
    public double GraphMaxValue { get => GetValue(GraphMaxValueProperty); set => SetValue(GraphMaxValueProperty, value); }

    /// <summary>Minimum width of the compact value text, so wider values (network rates) don't make the overlay jitter.</summary>
    public double ValueMinWidth { get => GetValue(ValueMinWidthProperty); set => SetValue(ValueMinWidthProperty, value); }

    public MetricSeverity Severity { get => GetValue(SeverityProperty); set => SetValue(SeverityProperty, value); }

    public bool HasSecondaryText => !string.IsNullOrEmpty(SecondaryText);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SecondaryTextProperty)
        {
            RaisePropertyChanged(HasSecondaryTextProperty, !string.IsNullOrEmpty((string?)change.OldValue), HasSecondaryText);
        }
        else if (change.Property == SeverityProperty)
        {
            ApplySeverity((MetricSeverity)change.NewValue!);
        }
    }

    private void ApplySeverity(MetricSeverity severity)
    {
        foreach (var element in new Control?[] { this.FindControl<Control>("CompactValue"), this.FindControl<Control>("DetailedValue"),
                     this.FindControl<Control>("CompactGraph"), this.FindControl<Control>("DetailedGraph") })
        {
            if (element is null)
            {
                continue;
            }

            element.Classes.Set("warning", severity == MetricSeverity.Warning);
            element.Classes.Set("critical", severity == MetricSeverity.Critical);
        }
    }
}
