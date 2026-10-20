using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using static HydroExplorer.App.Controls.HullDrawing;  // DrawLabel, Caption, Format

namespace HydroExplorer.App.Controls;

/// <summary>
/// Draws the box hull (L × B × D) as a pseudo-3D isometric "glass box": hidden edges dashed, the
/// submerged part of the visible faces shaded, and the waterplane at <see cref="Draft"/> as a
/// translucent horizontal plane.
/// </summary>
/// <remarks>
/// Same approach as <see cref="HullProfileView"/>: StyledProperties marked AffectsRender, and a
/// Render override. The 3D → 2D math lives in <see cref="HullIsometricLayout"/>; this class only
/// joins projected points with lines and filled polygons. No 3D engine is involved.
/// </remarks>
public sealed class HullIsometricView : Control
{
    // ---- Styled properties (see HullProfileView for the DependencyProperty comparison) ----

    public static readonly StyledProperty<double> LengthProperty =
        AvaloniaProperty.Register<HullIsometricView, double>(nameof(Length));

    public static readonly StyledProperty<double> BeamProperty =
        AvaloniaProperty.Register<HullIsometricView, double>(nameof(Beam));

    public static readonly StyledProperty<double> DepthProperty =
        AvaloniaProperty.Register<HullIsometricView, double>(nameof(Depth));

    /// <summary>Null means "no row selected": the box is drawn without a waterplane.</summary>
    public static readonly StyledProperty<double?> DraftProperty =
        AvaloniaProperty.Register<HullIsometricView, double?>(nameof(Draft));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<HullIsometricView>();

    // ---- Water brushes ----
    //
    // Set by the "controls|HullIsometricView" style in AppStyles.axaml from the palette's water
    // brushes, so they repaint on a light/dark theme switch (see HullProfileView for the same pattern).

    public static readonly StyledProperty<IBrush?> SubmergedBrushProperty =
        AvaloniaProperty.Register<HullIsometricView, IBrush?>(nameof(SubmergedBrush));

    public static readonly StyledProperty<IBrush?> WaterlineBrushProperty =
        AvaloniaProperty.Register<HullIsometricView, IBrush?>(nameof(WaterlineBrush));

    public static readonly StyledProperty<IBrush?> WaterplaneBrushProperty =
        AvaloniaProperty.Register<HullIsometricView, IBrush?>(nameof(WaterplaneBrush));

    static HullIsometricView()
    {
        AffectsRender<HullIsometricView>(
            LengthProperty, BeamProperty, DepthProperty, DraftProperty, ForegroundProperty,
            SubmergedBrushProperty, WaterlineBrushProperty, WaterplaneBrushProperty);
    }

    /// <summary>Hull length L (m).</summary>
    public double Length
    {
        get => GetValue(LengthProperty);
        set => SetValue(LengthProperty, value);
    }

    /// <summary>Hull beam B (m).</summary>
    public double Beam
    {
        get => GetValue(BeamProperty);
        set => SetValue(BeamProperty, value);
    }

    /// <summary>Hull depth D (m).</summary>
    public double Depth
    {
        get => GetValue(DepthProperty);
        set => SetValue(DepthProperty, value);
    }

    /// <summary>Draft T (m) of the waterplane, or null for none.</summary>
    public double? Draft
    {
        get => GetValue(DraftProperty);
        set => SetValue(DraftProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>Darker fill for the submerged part of the two visible faces.</summary>
    public IBrush? SubmergedBrush
    {
        get => GetValue(SubmergedBrushProperty);
        set => SetValue(SubmergedBrushProperty, value);
    }

    /// <summary>Stroke for the waterplane's edges.</summary>
    public IBrush? WaterlineBrush
    {
        get => GetValue(WaterlineBrushProperty);
        set => SetValue(WaterlineBrushProperty, value);
    }

    /// <summary>Translucent fill for the horizontal waterplane.</summary>
    public IBrush? WaterplaneBrush
    {
        get => GetValue(WaterplaneBrushProperty);
        set => SetValue(WaterplaneBrushProperty, value);
    }

    // ---- Drawing ----

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // Short upper-case names so the drawing code reads like the formula table (L, B, D, T).
        double L = Length, B = Beam, D = Depth;
        HullIsometricLayout? layout = HullIsometricLayout.Create(Bounds.Size, L, B, D);
        if (layout is null)
        {
            return;
        }

        // Foreground and the water brushes come from the "controls|HullIsometricView" style in
        // AppStyles.axaml, so they are only null before the control is styled — nothing to draw yet.
        if (Foreground is not { } foreground)
        {
            return;
        }

        var outlinePen = new Pen(foreground, 2);
        var hiddenPen = new Pen(foreground, 1, new DashStyle(new double[] { 4, 3 }, 0));
        double? draft = Draft is double t ? Math.Clamp(t, 0, D) : null;

        // A local function: a small helper that is only visible inside Render (like a closure in JS).
        Point P(double x, double y, double z) => layout.Project(x, y, z);

        // 1. Hidden edges (behind the box), dashed: the three edges meeting at the far keel corner.
        context.DrawLine(hiddenPen, P(L, B, 0), P(0, B, 0));
        context.DrawLine(hiddenPen, P(L, B, 0), P(L, 0, 0));
        context.DrawLine(hiddenPen, P(L, B, 0), P(L, B, D));

        if (draft is double T && SubmergedBrush is { } submerged && WaterlineBrush is { } waterline && WaterplaneBrush is { } waterplane)
        {
            var waterlinePen = new Pen(waterline, 2);
            var thinWaterPen = new Pen(waterline, 1);

            // 2. Submerged part of the two visible faces: the side (y = 0) and the end (x = 0).
            context.DrawGeometry(submerged, null, Polygon(P(0, 0, 0), P(L, 0, 0), P(L, 0, T), P(0, 0, T)));
            context.DrawGeometry(submerged, null, Polygon(P(0, 0, 0), P(0, B, 0), P(0, B, T), P(0, 0, T)));

            // 3. The waterplane: a translucent horizontal plane at z = T, the full L × B of the hull.
            context.DrawGeometry(waterplane, null, Polygon(P(0, 0, T), P(L, 0, T), P(L, B, T), P(0, B, T)));

            // 4. Its edges: strong on the visible faces, thin on the far side.
            context.DrawLine(waterlinePen, P(0, B, T), P(0, 0, T));
            context.DrawLine(waterlinePen, P(0, 0, T), P(L, 0, T));
            context.DrawLine(thinWaterPen, P(L, 0, T), P(L, B, T));
            context.DrawLine(thinWaterPen, P(L, B, T), P(0, B, T));
        }

        // 5. Visible edges: the deck outline, the three near vertical edges and the two near keel edges.
        context.DrawGeometry(null, outlinePen, Polygon(P(0, 0, D), P(L, 0, D), P(L, B, D), P(0, B, D)));
        context.DrawLine(outlinePen, P(0, 0, 0), P(0, 0, D));
        context.DrawLine(outlinePen, P(L, 0, 0), P(L, 0, D));
        context.DrawLine(outlinePen, P(0, B, 0), P(0, B, D));
        context.DrawLine(outlinePen, P(0, 0, 0), P(L, 0, 0));
        context.DrawLine(outlinePen, P(0, 0, 0), P(0, B, 0));

        // 6. Labels: the caption above the drawing, and each dimension beside its edge.
        DrawLabel(context, Caption(draft), new Point(Bounds.Width / 2, 14), foreground, HorizontalAlignment.Center);
        DrawLabel(context, Format($"L = {L:0.##} m"), Below(P(L / 2, 0, 0)), foreground, HorizontalAlignment.Left);
        DrawLabel(context, Format($"B = {B:0.##} m"), Below(P(0, B / 2, 0)), foreground, HorizontalAlignment.Right);
        DrawLabel(context, Format($"D = {D:0.##} m"), P(0, B, D / 2) + new Point(-8, 0), foreground, HorizontalAlignment.Right);
    }

    /// <summary>A point just below a keel edge, where its dimension label goes.</summary>
    private static Point Below(Point edgeMiddle) => edgeMiddle + new Point(0, 14);

    /// <summary>A closed, filled shape through the given screen points.</summary>
    /// <remarks>
    /// WPF note: WPF would build this with a PathGeometry/PathFigure (or StreamGeometry). Avalonia has
    /// PolylineGeometry, which takes the points directly.
    /// </remarks>
    private static Geometry Polygon(params Point[] corners) => new PolylineGeometry(corners, isFilled: true);
}
