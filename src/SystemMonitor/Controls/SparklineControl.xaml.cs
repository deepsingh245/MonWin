using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SystemMonitor.Controls;

/// <summary>
/// Lightweight rolling-history sparkline. Renders directly via OnRender (no charting
/// library, no per-frame animation) — it only repaints when new Values are pushed in,
/// auto-scaling to the buffer's own min/max so it stays readable at a few pixels tall.
/// </summary>
public partial class SparklineControl : UserControl
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(double[]), typeof(SparklineControl),
        new FrameworkPropertyMetadata(Array.Empty<double>(), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(
        nameof(LineBrush), typeof(Brush), typeof(SparklineControl),
        new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillBrushProperty = DependencyProperty.Register(
        nameof(FillBrush), typeof(Brush), typeof(SparklineControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaxValueProperty = DependencyProperty.Register(
        nameof(MaxValue), typeof(double), typeof(SparklineControl),
        new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double[] Values
    {
        get => (double[])GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush LineBrush
    {
        get => (Brush)GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public Brush? FillBrush
    {
        get => (Brush?)GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    /// <summary>Fixed scale ceiling (e.g. 100 for percentages). Values above this clamp visually.</summary>
    public double MaxValue
    {
        get => (double)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    public SparklineControl()
    {
        InitializeComponent();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var values = Values;
        var width = ActualWidth;
        var height = ActualHeight;

        if (values is null || values.Length < 2 || width <= 0 || height <= 0)
        {
            return;
        }

        var max = Math.Max(MaxValue, values.Max());
        var min = Math.Min(0.0, values.Min());
        var range = Math.Max(max - min, 0.0001);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var stepX = width / (values.Length - 1);
            Point PointAt(int i)
            {
                var normalized = (values[i] - min) / range;
                var y = height - normalized * height;
                return new Point(i * stepX, y);
            }

            ctx.BeginFigure(PointAt(0), FillBrush is not null, false);
            var linePoints = new Point[values.Length - 1];
            for (var i = 1; i < values.Length; i++)
            {
                linePoints[i - 1] = PointAt(i);
            }

            ctx.PolyLineTo(linePoints, true, true);

            if (FillBrush is not null)
            {
                ctx.LineTo(new Point(width, height), true, false);
                ctx.LineTo(new Point(0, height), true, false);
            }
        }

        geometry.Freeze();

        if (FillBrush is not null)
        {
            drawingContext.DrawGeometry(FillBrush, null, geometry);
        }

        drawingContext.DrawGeometry(null, new Pen(LineBrush, 1.25), geometry);
    }
}
