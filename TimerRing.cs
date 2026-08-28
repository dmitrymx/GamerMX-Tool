using System.Windows;
using System.Windows.Media;

namespace GamerMX.Tool;

public enum TimerFaceStyle
{
    NeonArc,
    SegmentedHalo,
    DualOrbit,
    Reactor,
    ChronoDots,
    Sweep,
    PulseCore,
    HexCore,
    Minimal
}

public sealed class TimerRing : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress),
        typeof(double),
        typeof(TimerRing),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender, null, CoerceProgress));

    public static readonly DependencyProperty FaceProperty = DependencyProperty.Register(
        nameof(Face),
        typeof(TimerFaceStyle),
        typeof(TimerRing),
        new FrameworkPropertyMetadata(TimerFaceStyle.NeonArc, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PhaseProperty = DependencyProperty.Register(
        nameof(Phase),
        typeof(double),
        typeof(TimerRing),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Brush TrackBrush = CreateBrush(Color.FromArgb(30, 255, 255, 255));
    private static readonly Brush TrackStrongBrush = CreateBrush(Color.FromArgb(58, 255, 255, 255));
    private static readonly Brush GlowBrush = CreateBrush(Color.FromArgb(50, 139, 92, 246));
    private static readonly Brush VioletBrush = CreateBrush(Color.FromRgb(167, 139, 250));
    private static readonly Brush DotBrush = CreateBrush(Color.FromRgb(94, 234, 212));
    private static readonly Brush PinkBrush = CreateBrush(Color.FromRgb(249, 168, 212));
    private static readonly LinearGradientBrush ArcBrush = CreateArcBrush();
    private static readonly Pen TrackPen = CreatePen(TrackBrush, 6);
    private static readonly Pen ThinTrackPen = CreatePen(TrackBrush, 1.4);
    private static readonly Pen GlowPen = CreatePen(GlowBrush, 16);
    private static readonly Pen ArcPen = CreatePen(ArcBrush, 6);
    private static readonly Pen ThinArcPen = CreatePen(ArcBrush, 2.5);
    private static readonly Pen VioletPen = CreatePen(VioletBrush, 3);
    private static readonly Pen CyanPen = CreatePen(DotBrush, 3);
    private static readonly Pen PinkPen = CreatePen(PinkBrush, 2);

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public TimerFaceStyle Face
    {
        get => (TimerFaceStyle)GetValue(FaceProperty);
        set => SetValue(FaceProperty, value);
    }

    public double Phase
    {
        get => (double)GetValue(PhaseProperty);
        set => SetValue(PhaseProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0)
            return;

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var radius = Math.Max(0, size / 2 - 15);
        var progress = Math.Clamp(Progress, 0, 1);

        switch (Face)
        {
            case TimerFaceStyle.SegmentedHalo:
                DrawSegmentedHalo(drawingContext, center, radius, progress);
                break;
            case TimerFaceStyle.DualOrbit:
                DrawDualOrbit(drawingContext, center, radius, progress);
                break;
            case TimerFaceStyle.Reactor:
                DrawReactor(drawingContext, center, radius, progress);
                break;
            case TimerFaceStyle.ChronoDots:
                DrawChronoDots(drawingContext, center, radius, progress);
                break;
            case TimerFaceStyle.Sweep:
                DrawSweep(drawingContext, center, radius, progress);
                break;
            case TimerFaceStyle.PulseCore:
                DrawPulseCore(drawingContext, center, radius, progress);
                break;
            case TimerFaceStyle.HexCore:
                DrawHexCore(drawingContext, center, radius, progress);
                break;
            case TimerFaceStyle.Minimal:
                DrawMinimal(drawingContext, center, radius, progress);
                break;
            default:
                DrawNeonArc(drawingContext, center, radius, progress);
                break;
        }
    }

    private static void DrawNeonArc(DrawingContext dc, Point center, double radius, double progress)
    {
        dc.DrawEllipse(null, TrackPen, center, radius, radius);
        if (progress <= .0001)
            return;
        DrawProgressArc(dc, center, radius, progress, true, ArcPen);
    }

    private static void DrawSegmentedHalo(DrawingContext dc, Point center, double radius, double progress)
    {
        const int segments = 48;
        var active = (int)Math.Ceiling(progress * segments);
        for (var i = 0; i < segments; i++)
        {
            var angle = -90 + i * 360d / segments;
            var start = PointAt(center, radius - 9, angle);
            var end = PointAt(center, radius, angle);
            dc.DrawLine(i < active ? (i % 4 == 0 ? CyanPen : VioletPen) : ThinTrackPen, start, end);
        }
        var index = Math.Clamp(active - 1, 0, segments - 1);
        dc.DrawEllipse(DotBrush, null, PointAt(center, radius - 4, -90 + index * 360d / segments), 4, 4);
    }

    private void DrawDualOrbit(DrawingContext dc, Point center, double radius, double progress)
    {
        dc.DrawEllipse(null, ThinTrackPen, center, radius, radius);
        DrawProgressArc(dc, center, radius, progress, false, ThinArcPen);
        dc.DrawEllipse(null, ThinTrackPen, center, radius - 14, (radius - 14) * .58);
        var a1 = Phase * 42 - 90;
        var a2 = -Phase * 31 + 90;
        dc.DrawEllipse(DotBrush, null,
            new Point(center.X + Math.Cos(a1 * Math.PI / 180) * (radius - 14),
                center.Y + Math.Sin(a1 * Math.PI / 180) * (radius - 14) * .58), 4.5, 4.5);
        dc.DrawEllipse(PinkBrush, null,
            new Point(center.X + Math.Cos(a2 * Math.PI / 180) * (radius - 14),
                center.Y + Math.Sin(a2 * Math.PI / 180) * (radius - 14) * .58), 3.5, 3.5);
    }

    private void DrawReactor(DrawingContext dc, Point center, double radius, double progress)
    {
        dc.DrawEllipse(null, ThinTrackPen, center, radius, radius);
        DrawProgressArc(dc, center, radius, progress, true, ArcPen);
        DrawArcRange(dc, center, radius - 13, Phase * 22 - 90, 112, CyanPen);
        DrawArcRange(dc, center, radius - 24, -Phase * 29 + 70, 82, PinkPen);
        DrawArcRange(dc, center, radius - 24, -Phase * 29 + 250, 82, PinkPen);
    }

    private static void DrawChronoDots(DrawingContext dc, Point center, double radius, double progress)
    {
        const int count = 60;
        var active = (int)Math.Ceiling(progress * count);
        for (var i = 0; i < count; i++)
        {
            var point = PointAt(center, radius, -90 + i * 6);
            var major = i % 5 == 0;
            var dotRadius = major ? 3.2 : 1.7;
            var brush = i < active ? (major ? DotBrush : VioletBrush) : TrackStrongBrush;
            dc.DrawEllipse(brush, null, point, dotRadius, dotRadius);
        }
    }

    private void DrawSweep(DrawingContext dc, Point center, double radius, double progress)
    {
        dc.DrawEllipse(null, ThinTrackPen, center, radius, radius);
        DrawProgressArc(dc, center, radius, progress, false, ArcPen);
        if (progress <= 0)
            return;
        var end = -90 + 359.999 * progress;
        for (var i = 1; i <= 5; i++)
        {
            dc.PushOpacity((6 - i) / 9d);
            DrawArcRange(dc, center, radius - i * 3, end - 15 - i * 4 + Math.Sin(Phase * 3) * 3, 12, CyanPen);
            dc.Pop();
        }
    }

    private void DrawPulseCore(DrawingContext dc, Point center, double radius, double progress)
    {
        dc.DrawEllipse(null, TrackPen, center, radius, radius);
        DrawProgressArc(dc, center, radius, progress, false, ArcPen);
        var pulse = (Math.Sin(Phase * 2.8) + 1) / 2;
        dc.PushOpacity((1 - pulse) * .42);
        dc.DrawEllipse(null, GlowPen, center, radius * (.55 + pulse * .28), radius * (.55 + pulse * .28));
        dc.Pop();
        dc.DrawEllipse(VioletBrush, null, center, 3.5, 3.5);
    }

    private static void DrawHexCore(DrawingContext dc, Point center, double radius, double progress)
    {
        var points = Enumerable.Range(0, 6).Select(i => PointAt(center, radius, -90 + i * 60)).ToArray();
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(points[0], false, true);
            context.PolyLineTo(points.Skip(1).ToArray(), true, false);
        }
        geometry.Freeze();
        dc.DrawGeometry(null, ThinTrackPen, geometry);
        DrawProgressArc(dc, center, radius * .82, progress, false, ThinArcPen);
        var activeNodes = (int)Math.Ceiling(progress * 6);
        for (var i = 0; i < points.Length; i++)
            dc.DrawEllipse(i < activeNodes ? (i % 2 == 0 ? DotBrush : VioletBrush) : TrackStrongBrush,
                null, points[i], 4, 4);
    }

    private static void DrawMinimal(DrawingContext dc, Point center, double radius, double progress)
    {
        dc.DrawEllipse(null, ThinTrackPen, center, radius, radius);
        DrawProgressArc(dc, center, radius, progress, false, ThinArcPen);
    }

    private static void DrawProgressArc(
        DrawingContext dc,
        Point center,
        double radius,
        double progress,
        bool glow,
        Pen pen)
    {
        if (progress <= .0001)
            return;
        var endAngle = -90 + 359.999 * progress;
        var geometry = CreateArc(center, radius, -90, endAngle, progress > .5);
        if (glow)
            dc.DrawGeometry(null, GlowPen, geometry);
        dc.DrawGeometry(null, pen, geometry);
        var endpoint = PointAt(center, radius, endAngle);
        if (glow)
            dc.DrawEllipse(GlowBrush, null, endpoint, 9, 9);
        dc.DrawEllipse(DotBrush, null, endpoint, 4, 4);
    }

    private static void DrawArcRange(
        DrawingContext dc,
        Point center,
        double radius,
        double startAngle,
        double sweep,
        Pen pen)
    {
        var geometry = CreateArc(center, radius, startAngle, startAngle + sweep, sweep > 180);
        dc.DrawGeometry(null, pen, geometry);
    }

    private static Point PointAt(Point center, double radius, double angle)
    {
        var radians = angle * Math.PI / 180d;
        return new Point(
            center.X + radius * Math.Cos(radians),
            center.Y + radius * Math.Sin(radians));
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
