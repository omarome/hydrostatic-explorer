using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using HydroExplorer.Core.Models;
using static HydroExplorer.App.Controls.HullDrawing;  // DrawLabel, Caption, Format

namespace HydroExplorer.App.Controls;

/// <summary>
/// The hull's body plan: its cross-sections at stations 1–9, aft half on the left and forward half on the
/// right, with the waterline at <see cref="Draft"/>. A classic naval-architecture view of the hull's shape.
/// </summary>
/// <remarks>
/// Same pattern as <see cref="HullProfileView"/>: StyledProperties bound from <c>HullViewModel</c>, brushes set
/// from the palette by a style, layout math in <see cref="BodyPlanLayout"/>. The sections come from
/// <c>HullGeometry.HalfBreadth</c>, the same function the hydrostatics integrate.
/// </remarks>
public sealed class BodyPlanView : Control
{
    public static readonly StyledProperty<double> LengthProperty =
        AvaloniaProperty.Register<BodyPlanView, double>(nameof(Length));

    public static readonly StyledProperty<double> BeamProperty =
        AvaloniaProperty.Register<BodyPlanView, double>(nameof(Beam));

    public static readonly StyledProperty<double> DepthProperty =
        AvaloniaProperty.Register<BodyPlanView, double>(nameof(Depth));

    /// <summary>Null for a box barge (every section is the same rectangle).</summary>
    public static readonly StyledProperty<HullShape?> ShapeProperty =
        AvaloniaProperty.Register<BodyPlanView, HullShape?>(nameof(Shape));

    /// <summary>Null means "no draft selected": no waterline.</summary>
    public static readonly StyledProperty<double?> DraftProperty =
        AvaloniaProperty.Register<BodyPlanView, double?>(nameof(Draft));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<BodyPlanView>();

    /// <summary>Water tint below the waterline (set from the palette by the "controls|BodyPlanView" style).</summary>
    public static readonly StyledProperty<IBrush?> SeaBrushProperty =
        AvaloniaProperty.Register<BodyPlanView, IBrush?>(nameof(SeaBrush));

    public static readonly StyledProperty<IBrush?> WaterlineBrushProperty =
        AvaloniaProperty.Register<BodyPlanView, IBrush?>(nameof(WaterlineBrush));

    static BodyPlanView()
    {
        AffectsRender<BodyPlanView>(
            LengthProperty, BeamProperty, DepthProperty, ShapeProperty, DraftProperty,
            ForegroundProperty, SeaBrushProperty, WaterlineBrushProperty);
    }

    public double Length { get => GetValue(LengthProperty); set => SetValue(LengthProperty, value); }

    public double Beam { get => GetValue(BeamProperty); set => SetValue(BeamProperty, value); }

    public double Depth { get => GetValue(DepthProperty); set => SetValue(DepthProperty, value); }

    public HullShape? Shape { get => GetValue(ShapeProperty); set => SetValue(ShapeProperty, value); }

    public double? Draft { get => GetValue(DraftProperty); set => SetValue(DraftProperty, value); }

    /// <summary>Section lines and labels.</summary>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public IBrush? SeaBrush { get => GetValue(SeaBrushProperty); set => SetValue(SeaBrushProperty, value); }

    public IBrush? WaterlineBrush { get => GetValue(WaterlineBrushProperty); set => SetValue(WaterlineBrushProperty, value); }

    // ---- Drawing ----

    private const double LineOverhang = 12;  // how far the waterline, baseline and centreline reach past the sections (px)

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        BodyPlanLayout? layout = BodyPlanLayout.Create(Bounds.Size, Beam, Depth);
        if (layout is null || Length <= 0 || Foreground is not { } foreground)
        {
            return;
        }

        double halfBeam = Beam / 2;
        double? draft = Draft is double t && t > 0 && t <= Depth ? t : null;
        var sectionPen = new Pen(foreground, 1);
        var midshipPen = new Pen(foreground, 2);
        var dashedPen = new Pen(foreground, 1, new DashStyle(new double[] { 4, 3 }, 0));
        Point keel = layout.ToScreen(0, 0);
        Point leftDeck = layout.ToScreen(-halfBeam, Depth), rightDeck = layout.ToScreen(halfBeam, Depth);

        // 1. Water below the waterline, across the full beam (and a little past it).
        if (draft is double waterline && SeaBrush is { } sea)
        {
            Point topLeft = layout.ToScreen(-halfBeam, waterline);
            context.FillRectangle(sea, new Rect(
                topLeft.X - LineOverhang, topLeft.Y, rightDeck.X - leftDeck.X + 2 * LineOverhang, keel.Y - topLeft.Y));
        }

        // 2. Sections: one polyline per station. Midships (station 5) is drawn heavier, as on a lines plan.
        var hull = new Hull(0, string.Empty, Length, Beam, Depth, Shape);
        foreach (BodyPlanSection section in BodyPlanLayout.Sections(hull))
        {
            Point[] points = section.Points.Select(p => layout.ToScreen(p.Y, p.Z)).ToArray();
            IPen pen = section.Station == BodyPlanLayout.StationIntervals / 2 ? midshipPen : sectionPen;
            context.DrawGeometry(null, pen, new PolylineGeometry(points, isFilled: false));
        }

        // 3. Centreline (CL) and baseline (BL), dashed like the side view's baseline.
        context.DrawLine(dashedPen, new Point(keel.X, keel.Y + LineOverhang), new Point(keel.X, leftDeck.Y - LineOverhang));
        DrawLabel(context, "CL", new Point(keel.X, leftDeck.Y - LineOverhang - 8), foreground, HorizontalAlignment.Center);
        context.DrawLine(dashedPen, new Point(leftDeck.X - LineOverhang, keel.Y), new Point(rightDeck.X + LineOverhang, keel.Y));

        // 4. Waterline across both halves.
        if (draft is double wl && WaterlineBrush is { } waterlineBrush)
        {
            Point left = layout.ToScreen(-halfBeam, wl), right = layout.ToScreen(halfBeam, wl);
            context.DrawLine(new Pen(waterlineBrush, 2), new Point(left.X - LineOverhang, left.Y), new Point(right.X + LineOverhang, right.Y));
        }

        // 5. Labels: which half is which, the caption, and the beam below.
        DrawLabel(context, "Aft", new Point(leftDeck.X, leftDeck.Y - 10), foreground, HorizontalAlignment.Left);
        DrawLabel(context, "Fore", new Point(rightDeck.X, rightDeck.Y - 10), foreground, HorizontalAlignment.Right);
        DrawLabel(context, Caption(draft), new Point(keel.X, 12), foreground, HorizontalAlignment.Center);  // top edge, like the 3D view
        DrawLabel(context, Format($"B = {Beam:0.##} m · stations 1–9 of 10"), new Point(keel.X, keel.Y + 24), foreground, HorizontalAlignment.Center);
    }
}
