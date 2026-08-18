using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;

namespace NexusExplorer.App.Controls;

/// <summary>
/// Draws a circular (donut) progress indicator: a full track ring plus an arc
/// that grows clockwise from the top according to <see cref="Value"/> (0-100).
/// </summary>
public sealed class CircularProgressControl : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<CircularProgressControl, double>(nameof(Value), 0);

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<CircularProgressControl, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> ProgressBrushProperty =
        AvaloniaProperty.Register<CircularProgressControl, IBrush?>(nameof(ProgressBrush));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<CircularProgressControl, double>(nameof(StrokeThickness), 2);

    static CircularProgressControl()
    {
        AffectsRender<CircularProgressControl>(ValueProperty, TrackBrushProperty, ProgressBrushProperty, StrokeThicknessProperty);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public IBrush? ProgressBrush
    {
        get => GetValue(ProgressBrushProperty);
        set => SetValue(ProgressBrushProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0) return;

        var thickness = Math.Max(1, StrokeThickness);
        var radius = (size / 2) - (thickness / 2);
        if (radius <= 0) return;

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);

        // Background track ring.
        var trackPen = new Pen(TrackBrush, thickness);
        context.DrawEllipse(null, trackPen, center, radius, radius);

        var clamped = Math.Clamp(Value, 0, 100);
        if (clamped <= 0) return;

        var progressPen = new Pen(ProgressBrush, thickness, null, PenLineCap.Round);

        if (clamped >= 100)
        {
            context.DrawEllipse(null, progressPen, center, radius, radius);
            return;
        }

        var angle = 360.0 * (clamped / 100.0);
        var startAngle = -90.0;
        var endAngle = startAngle + angle;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(PointOnCircle(center, radius, startAngle), false);
            ctx.ArcTo(
                PointOnCircle(center, radius, endAngle),
                new Size(radius, radius),
                0,
                angle > 180,
                SweepDirection.Clockwise);
            ctx.EndFigure(false);
        }

        context.DrawGeometry(null, progressPen, geometry);
    }

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180.0;
        return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
    }
}
