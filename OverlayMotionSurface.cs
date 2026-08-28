using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace GamerMX.Tool;

public enum OverlayAnimationMode
{
    Aurora,
    Orbits,
    Radar,
    Pulse,
    Stardust,
    Calm
}

/// <summary>
/// One lightweight drawing surface for all overlay motion. No blur effects,
/// shadow bitmaps, particle controls or per-frame layout changes are used.
/// </summary>
public sealed class OverlayMotionSurface : FrameworkElement
{
    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode),
        typeof(OverlayAnimationMode),
        typeof(OverlayMotionSurface),
        new FrameworkPropertyMetadata(OverlayAnimationMode.Aurora, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Brush Violet = Solid(Color.FromArgb(54, 139, 92, 246));
    private static readonly Brush VioletSoft = Solid(Color.FromArgb(24, 139, 92, 246));
    private static readonly Brush Cyan = Solid(Color.FromArgb(72, 45, 212, 191));
    private static readonly Brush CyanSoft = Solid(Color.FromArgb(28, 45, 212, 191));
    private static readonly Brush WhiteSoft = Solid(Color.FromArgb(40, 255, 255, 255));
    private static readonly Pen VioletPen = FrozenPen(Violet, 2);
    private static readonly Pen VioletThinPen = FrozenPen(VioletSoft, 1);
    private static readonly Pen CyanPen = FrozenPen(Cyan, 2);
    private static readonly Pen CyanThinPen = FrozenPen(CyanSoft, 1);
    private static readonly RadialGradientBrush AuroraViolet = Radial(
        Color.FromArgb(62, 139, 92, 246), Color.FromArgb(0, 139, 92, 246));
    private static readonly RadialGradientBrush AuroraCyan = Radial(
        Color.FromArgb(44, 45, 212, 191), Color.FromArgb(0, 45, 212, 191));

    private readonly DispatcherTimer _frameTimer = new(DispatcherPriority.Background)
    {
        Interval = TimeSpan.FromMilliseconds(100)
    };
    private DateTime _startedAtUtc;
    private double _phase;

    public OverlayMotionSurface()
    {
        IsHitTestVisible = false;
        _frameTimer.Tick += (_, _) =>
        {
            _phase = (DateTime.UtcNow - _startedAtUtc).TotalSeconds;
            InvalidateVisual();
        };
    }

    public OverlayAnimationMode Mode
    {
        get => (OverlayAnimationMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public void Start()
    {
        _startedAtUtc = DateTime.UtcNow - TimeSpan.FromSeconds(_phase);
        if (Mode != OverlayAnimationMode.Calm)
            _frameTimer.Start();
        InvalidateVisual();
    }

    public void Stop() => _frameTimer.Stop();

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth <= 0 || ActualHeight <= 0)
            return;

        switch (Mode)
        {
            case OverlayAnimationMode.Aurora:
                DrawAurora(dc);
                break;
            case OverlayAnimationMode.Orbits:
                DrawOrbits(dc);
                break;
            case OverlayAnimationMode.Radar:
                DrawRadar(dc);
                break;
            case OverlayAnimationMode.Pulse:
                DrawPulse(dc);
                break;
            case OverlayAnimationMode.Stardust:
                DrawStardust(dc);
                break;
            default:
                DrawCalm(dc);
                break;
        }
    }

    private void DrawAurora(DrawingContext dc)
    {
        var scale = Math.Max(ActualWidth, ActualHeight);
        var x1 = ActualWidth * (.28 + Math.Sin(_phase * .28) * .08);
        var y1 = ActualHeight * (.42 + Math.Cos(_phase * .22) * .13);
        var x2 = ActualWidth * (.72 + Math.Cos(_phase * .25) * .09);
        var y2 = ActualHeight * (.55 + Math.Sin(_phase * .2) * .14);
        dc.DrawEllipse(AuroraViolet, null, new Point(x1, y1), scale * .42, scale * .26);
        dc.DrawEllipse(AuroraCyan, null, new Point(x2, y2), scale * .35, scale * .22);
        DrawCalm(dc);
    }

    private void DrawOrbits(DrawingContext dc)
    {
        var center = new Point(ActualWidth * .5, ActualHeight * .5);
        var baseRadius = Math.Min(ActualWidth, ActualHeight) * .28;
        for (var i = 0; i < 3; i++)
        {
            var radius = baseRadius * (1 + i * .42);
            dc.DrawEllipse(null, i % 2 == 0 ? VioletThinPen : CyanThinPen, center, radius * 1.8, radius);
            var angle = (_phase * (24 - i * 4) + i * 110) * Math.PI / 180;
            var dot = new Point(
                center.X + Math.Cos(angle) * radius * 1.8,
                center.Y + Math.Sin(angle) * radius);
            dc.DrawEllipse(i % 2 == 0 ? Cyan : Violet, null, dot, 3.5 + i, 3.5 + i);
        }
    }

    private void DrawRadar(DrawingContext dc)
    {
        var center = new Point(ActualWidth * .5, ActualHeight * .5);
        var radius = Math.Min(ActualWidth, ActualHeight) * .42;
        for (var i = 1; i <= 4; i++)
            dc.DrawEllipse(null, CyanThinPen, center, radius * i / 4, radius * i / 4);

        var angle = _phase * .72;
        var end = new Point(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius);
        dc.DrawLine(CyanPen, center, end);
        dc.PushOpacity(.15);
        dc.DrawEllipse(Cyan, null, end, 16, 16);
        dc.Pop();
    }

    private void DrawPulse(DrawingContext dc)
    {
        var center = new Point(ActualWidth * .5, ActualHeight * .5);
        var maxRadius = Math.Sqrt(ActualWidth * ActualWidth + ActualHeight * ActualHeight) * .45;
        for (var i = 0; i < 4; i++)
        {
            var p = (_phase * .25 + i * .25) % 1;
            dc.PushOpacity((1 - p) * .65);
            dc.DrawEllipse(null, i % 2 == 0 ? VioletPen : CyanPen, center, maxRadius * p, maxRadius * p);
            dc.Pop();
        }
    }

    private void DrawStardust(DrawingContext dc)
    {
        for (var i = 0; i < 26; i++)
        {
            var seedX = ((i * 73) % 101) / 100d;
            var seedY = ((i * 47) % 97) / 96d;
            var x = (seedX * ActualWidth + _phase * (4 + i % 4)) % Math.Max(1, ActualWidth);
            var y = seedY * ActualHeight + Math.Sin(_phase * .6 + i) * 8;
            var radius = 1.2 + i % 3;
            dc.PushOpacity(.25 + (Math.Sin(_phase + i * .7) + 1) * .25);
            dc.DrawEllipse(i % 4 == 0 ? Cyan : WhiteSoft, null, new Point(x, y), radius, radius);
            dc.Pop();
        }
    }

    private void DrawCalm(DrawingContext dc)
    {
        var spacing = Math.Max(54, Math.Min(ActualWidth, ActualHeight) * .18);
        dc.PushOpacity(.28);
        for (var x = -ActualHeight; x < ActualWidth; x += spacing)
            dc.DrawLine(VioletThinPen, new Point(x, ActualHeight), new Point(x + ActualHeight, 0));
        dc.Pop();
    }

    private static Brush Solid(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Pen FrozenPen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        pen.Freeze();
        return pen;
    }

    private static RadialGradientBrush Radial(Color center, Color edge)
    {
        var brush = new RadialGradientBrush(center, edge)
        {
            RadiusX = .5,
            RadiusY = .5
        };
        brush.Freeze();
        return brush;
    }
}
