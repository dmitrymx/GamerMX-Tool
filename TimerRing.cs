using System.Windows;
using System.Windows.Media;

namespace GamerMX.Tool;

/// <summary>
/// Lightweight circular progress indicator. It draws directly through
/// DrawingContext and has no Shape tree, blur, bitmap cache or layout animation.
/// </summary>
public sealed class TimerRing : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress),
        typeof(double),
        typeof(TimerRing),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender, null, CoerceProgress));

    private static readonly Brush TrackBrush = CreateBrush(Color.FromArgb(30, 255, 255, 255));
    private static readonly Brush GlowBrush = CreateBrush(Color.FromArgb(50, 139, 92, 246));
    private static readonly Brush DotBrush = CreateBrush(Color.FromRgb(94, 234, 212));
    private static readonly LinearGradientBrush ArcBrush = CreateArcBrush();
    private static readonly Pen TrackPen = CreatePen(TrackBrush, 6);
    private static readonly Pen GlowPen = CreatePen(GlowBrush, 16);
    private static readonly Pen ArcPen = CreatePen(ArcBrush, 6);

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0)
            return;

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var radius = Math.Max(0, size / 2 - 15);
        drawingContext.DrawEllipse(null, TrackPen, center, radius, radius);

        var progress = Math.Clamp(Progress, 0, 1);
        if (progress <= .0001)
            return;

        var startAngle = -90d;
        var endAngle = startAngle + 359.999d * progress;
        var geometry = CreateArc(center, radius, startAngle, endAngle, progress > .5);

        drawingContext.DrawGeometry(null, GlowPen, geometry);
        drawingContext.DrawGeometry(null, ArcPen, geometry);

        var endRadians = endAngle * Math.PI / 180d;
        var endpoint = new Point(
            center.X + radius * Math.Cos(endRadians),
            center.Y + radius * Math.Sin(endRadians));
        drawingContext.DrawEllipse(GlowBrush, null, endpoint, 9, 9);
        drawingContext.DrawEllipse(DotBrush, null, endpoint, 4, 4);
    }

    private static object CoerceProgress(DependencyObject d, object value) =>
        Math.Clamp((double)value, 0, 1);

    private static StreamGeometry CreateArc(
        Point center,
        double radius,
        double startAngle,
        double endAngle,
        bool isLargeArc)
    {
        var startRadians = startAngle * Math.PI / 180d;
        var endRadians = endAngle * Math.PI / 180d;
        var start = new Point(
            center.X + radius * Math.Cos(startRadians),
            center.Y + radius * Math.Sin(startRadians));
        var end = new Point(
            center.X + radius * Math.Cos(endRadians),
            center.Y + radius * Math.Sin(endRadians));

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(start, false, false);
            context.ArcTo(
                end,
                new Size(radius, radius),
                0,
                isLargeArc,
                SweepDirection.Clockwise,
                true,
                false);
        }
        geometry.Freeze();
        return geometry;
    }

    private static Pen CreatePen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        pen.Freeze();
        return pen;
    }

    private static Brush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static LinearGradientBrush CreateArcBrush()
    {
        var brush = new LinearGradientBrush(
            Color.FromRgb(167, 139, 250),
            Color.FromRgb(45, 212, 191),
            new Point(0, 0),
            new Point(1, 1));
        brush.Freeze();
        return brush;
    }
}
