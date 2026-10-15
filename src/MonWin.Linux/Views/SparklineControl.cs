using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MonWin.Linux.Views;

/// <summary>
/// Lightweight rolling-history sparkline. Draws directly in Render (no charting library, no
/// animation timer): it repaints only when new values arrive.
/// </summary>
public sealed class SparklineControl : Control
{
    public static readonly StyledProperty<double[]?> ValuesProperty =
        AvaloniaProperty.Register<SparklineControl, double[]?>(nameof(Values));

    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<SparklineControl, IBrush?>(nameof(LineBrush), Brushes.DodgerBlue);

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<SparklineControl, IBrush?>(nameof(FillBrush));

    public static readonly StyledProperty<double> MaxValueProperty =
        AvaloniaProperty.Register<SparklineControl, double>(nameof(MaxValue), 100.0);

    static SparklineControl()
    {
        AffectsRender<SparklineControl>(ValuesProperty, LineBrushProperty, FillBrushProperty, MaxValueProperty);
    }

    public double[]? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public IBrush? LineBrush
    {
        get => GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public IBrush? FillBrush
    {
        get => GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    /// <summary>Scale ceiling: 100 for percentages; 0 auto-scales to the history's own maximum (byte rates).</summary>
    public double MaxValue
    {
        get => GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var points = SparklineGeometry.ComputePoints(Values, Bounds.Width, Bounds.Height, MaxValue);
        if (points.Length < 2)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(new Point(points[0].X, points[0].Y), FillBrush is not null);
            for (var i = 1; i < points.Length; i++)
            {
                g.LineTo(new Point(points[i].X, points[i].Y));
            }

            if (FillBrush is not null)
            {
                g.LineTo(new Point(Bounds.Width, Bounds.Height));
                g.LineTo(new Point(0, Bounds.Height));
            }

            g.EndFigure(isClosed: FillBrush is not null);
        }

        if (FillBrush is not null)
        {
            context.DrawGeometry(FillBrush, null, geometry);
        }

        if (LineBrush is not null)
        {
            // Stroke only the data polyline (not the closing edges used for the fill).
            var line = new StreamGeometry();
            using (var g = line.Open())
            {
                g.BeginFigure(new Point(points[0].X, points[0].Y), isFilled: false);
                for (var i = 1; i < points.Length; i++)
                {
                    g.LineTo(new Point(points[i].X, points[i].Y));
                }

                g.EndFigure(isClosed: false);
            }

            context.DrawGeometry(null, new Pen(LineBrush, 1.25), line);
        }
    }
}
