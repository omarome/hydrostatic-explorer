using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using HydroExplorer.App.Rendering;
using HydroExplorer.Core.Models;
using static HydroExplorer.App.Controls.HullDrawing;  // DrawLabel, Caption, Format

namespace HydroExplorer.App.Controls;

/// <summary>
/// Draws a box hull's side profile (L × D) with the keel baseline, dimension labels, and the
/// waterline at <see cref="Draft"/> with the submerged part shaded like water.
/// </summary>
/// <remarks>
/// A custom control draws itself by overriding <see cref="Render"/>, a bit like drawing on an HTML
/// canvas in React. The difference is that Avalonia calls Render again whenever a property listed in
/// AffectsRender changes or the control is resized, so we never schedule a redraw ourselves.
/// </remarks>
public sealed class HullProfileView : Control
{
    // ---- Styled properties ----
    //
    // A StyledProperty is a property that the UI framework knows about: it can be the target of a
    // {Binding}, set by a style, and change notifications are built in. The static field is the
    // property's identity; the CLR property below just wraps GetValue/SetValue.
    //
    // WPF note: this is Avalonia's version of a DependencyProperty. In WPF you would write
    //   public static readonly DependencyProperty LengthProperty = DependencyProperty.Register(
    //       nameof(Length), typeof(double), typeof(HullProfileView),
    //       new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
    // Avalonia's AvaloniaProperty.Register<TOwner, TValue> is generic (type-safe), and "affects render"
    // is declared once in the static constructor instead of in the metadata.

    public static readonly StyledProperty<double> LengthProperty =
        AvaloniaProperty.Register<HullProfileView, double>(nameof(Length));

    public static readonly StyledProperty<double> DepthProperty =
        AvaloniaProperty.Register<HullProfileView, double>(nameof(Depth));

    /// <summary>Null means "no row selected": the hull is drawn without a waterline.</summary>
    public static readonly StyledProperty<double?> DraftProperty =
        AvaloniaProperty.Register<HullProfileView, double?>(nameof(Draft));

    /// <summary>Beam B: only needed to build the superstructure boxes, shared with the 3D view.</summary>
    public static readonly StyledProperty<double> BeamProperty =
        AvaloniaProperty.Register<HullProfileView, double>(nameof(Beam));

    /// <summary>Null for a box barge; for a ship, its superstructure layout is outlined above the deck.</summary>
    public static readonly StyledProperty<HullShape?> ShapeProperty =
        AvaloniaProperty.Register<HullProfileView, HullShape?>(nameof(Shape));

    /// <summary>
    /// Reuses the inherited text Foreground, so lines and labels follow the light/dark theme.
    /// WPF note: the same idea as TextElement.ForegroundProperty.AddOwner in WPF.
    /// </summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<HullProfileView>();

    // ---- Water brushes ----
    //
    // These are plain StyledProperty<IBrush?> (not inherited like Foreground), set by the
    // "controls|HullProfileView" style in AppStyles.axaml from the palette's water brushes. Keeping
    // them as properties (rather than static fields, as before) is what lets them repaint on a
    // light/dark theme switch.

    public static readonly StyledProperty<IBrush?> SeaBrushProperty =
        AvaloniaProperty.Register<HullProfileView, IBrush?>(nameof(SeaBrush));

    public static readonly StyledProperty<IBrush?> SubmergedBrushProperty =
        AvaloniaProperty.Register<HullProfileView, IBrush?>(nameof(SubmergedBrush));

    public static readonly StyledProperty<IBrush?> WaterlineBrushProperty =
        AvaloniaProperty.Register<HullProfileView, IBrush?>(nameof(WaterlineBrush));

    public static readonly StyledProperty<IBrush?> SuperstructureFillBrushProperty =
        AvaloniaProperty.Register<HullProfileView, IBrush?>(nameof(SuperstructureFillBrush));

    static HullProfileView()
    {
        // Redraw (call Render again) whenever any of these properties change.
        // WPF note: FrameworkPropertyMetadataOptions.AffectsRender does this in WPF.
        AffectsRender<HullProfileView>(
            LengthProperty, DepthProperty, DraftProperty, BeamProperty, ShapeProperty, ForegroundProperty,
            SeaBrushProperty, SubmergedBrushProperty, WaterlineBrushProperty, SuperstructureFillBrushProperty);
    }

    /// <summary>Hull length L (m).</summary>
    public double Length
    {
        get => GetValue(LengthProperty);
        set => SetValue(LengthProperty, value);
    }

    /// <summary>Hull depth D (m).</summary>
    public double Depth
    {
        get => GetValue(DepthProperty);
        set => SetValue(DepthProperty, value);
    }

    /// <summary>Draft T (m) of the waterline, or null for none.</summary>
    public double? Draft
    {
        get => GetValue(DraftProperty);
        set => SetValue(DraftProperty, value);
    }

    /// <summary>Hull beam B (m).</summary>
    public double Beam
    {
        get => GetValue(BeamProperty);
        set => SetValue(BeamProperty, value);
    }

    /// <summary>The ship's hull form and superstructure layout, or null for a box barge.</summary>
    public HullShape? Shape
    {
        get => GetValue(ShapeProperty);
        set => SetValue(ShapeProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>Ambient water tint across the whole width, at and below the waterline.</summary>
    public IBrush? SeaBrush
    {
        get => GetValue(SeaBrushProperty);
        set => SetValue(SeaBrushProperty, value);
    }

    /// <summary>Darker fill for the part of the hull that is underwater.</summary>
    public IBrush? SubmergedBrush
    {
        get => GetValue(SubmergedBrushProperty);
        set => SetValue(SubmergedBrushProperty, value);
    }

    /// <summary>Light fill for the superstructure outline.</summary>
    public IBrush? SuperstructureFillBrush
    {
        get => GetValue(SuperstructureFillBrushProperty);
        set => SetValue(SuperstructureFillBrushProperty, value);
    }

    /// <summary>Stroke for the waterline itself.</summary>
    public IBrush? WaterlineBrush
    {
        get => GetValue(WaterlineBrushProperty);
        set => SetValue(WaterlineBrushProperty, value);
    }

    // ---- Drawing ----

    private const double LineOverhang = 16;  // how far waterline/baseline extend past the hull ends (px)
    private const double LabelOffset = 8;    // vertical gap between a line and its "WL"/"BL" label (px)

    // WPF note: WPF's equivalent is OnRender(DrawingContext). Both record drawing commands that the
    // framework replays; neither draws directly to the screen while the method runs.
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // The superstructure comes from the same boxes the 3D view uses; the layout leaves room above the deck for it.
        IReadOnlyList<SuperstructureBox> superstructure = Length > 0 && Beam > 0 && Depth > 0
            ? SuperstructureBoxes.For(new Hull(0, string.Empty, Length, Beam, Depth, Shape))
            : Array.Empty<SuperstructureBox>();
        double heightAboveDeck = SuperstructureBoxes.MaxHeightAboveDeck(Shape) * Depth;

        // Bounds.Size is the control's current size, so a resize naturally gives a new layout.
        HullProfileLayout? layout = HullProfileLayout.Create(Bounds.Size, Length, Depth, heightAboveDeck);
        if (layout is null)
        {
            return;
        }

        // Foreground and the water brushes come from the "controls|HullProfileView" style in
        // AppStyles.axaml, so they are only null before the control is styled (e.g. in a unit test
        // that never attaches it to a visual tree) — nothing to draw yet in that case.
        if (Foreground is not { } foreground)
        {
            return;
        }

        var outlinePen = new Pen(foreground, 2);
        var thinPen = new Pen(foreground, 1);
        var dashedPen = new Pen(foreground, 1, new DashStyle(new double[] { 4, 3 }, 0));
        Rect hull = layout.Hull;
        double? draft = Draft;
        double? waterlineY = draft is null ? null : layout.WaterlineY(draft.Value);

        // 1. Water: a light band across the whole width, darker where it fills the hull.
        if (waterlineY is double seaTop && SeaBrush is { } sea && SubmergedBrush is { } submerged)
        {
            double seaBottom = Math.Min(hull.Bottom + 12, Bounds.Height);
            context.FillRectangle(sea, new Rect(0, seaTop, Bounds.Width, seaBottom - seaTop));
            context.FillRectangle(submerged, new Rect(hull.Left, seaTop, hull.Width, hull.Bottom - seaTop));
        }

        // 2. Hull outline, then the superstructure seen from the side: each box is a rectangle from its aft
        //    to its fore end and from its bottom to its top.
        context.DrawRectangle(null, outlinePen, hull);
        foreach (SuperstructureBox box in superstructure)
        {
            var outline = new Rect(layout.ToScreen(box.Aft, box.Top), layout.ToScreen(box.Fore, box.Bottom));
            context.DrawRectangle(SuperstructureFillBrush, thinPen, outline);
        }

        // 3. Keel baseline (BL), extended past both ends. "BL" sits just below its line and "WL" just
        //    above its line, so the two labels never overlap at shallow drafts.
        double keelY = layout.KeelY;
        context.DrawLine(dashedPen, new Point(hull.Left - LineOverhang, keelY), new Point(hull.Right + LineOverhang, keelY));
        DrawLabel(context, "BL", new Point(hull.Right + LineOverhang + 4, keelY + LabelOffset), foreground, HorizontalAlignment.Left);

        // 4. Waterline (WL).
        if (waterlineY is double wl && WaterlineBrush is { } waterline)
        {
            var waterlinePen = new Pen(waterline, 2);
            context.DrawLine(waterlinePen, new Point(hull.Left - LineOverhang, wl), new Point(hull.Right + LineOverhang, wl));
            DrawLabel(context, "WL", new Point(hull.Right + LineOverhang + 4, wl - LabelOffset), foreground, HorizontalAlignment.Left);
        }

        // 5. Caption above the drawing (deck or superstructure): the current draft, or a hint. Up there it can't
        //    collide with the drawing, even when the hull is only a few pixels tall.
        double drawingTop = layout.ToScreen(0, Depth + heightAboveDeck).Y;
        DrawLabel(context, Caption(draft), new Point(hull.Center.X, Math.Max(drawingTop - 14, 8)), foreground, HorizontalAlignment.Center);

        // 6. Length dimension below the hull: |<—— L = 120 m ——>|
        double dimY = hull.Bottom + 28;
        context.DrawLine(thinPen, new Point(hull.Left, dimY), new Point(hull.Right, dimY));
        DrawTick(context, thinPen, new Point(hull.Left, dimY), vertical: true);
        DrawTick(context, thinPen, new Point(hull.Right, dimY), vertical: true);
        DrawLabel(context, Format($"L = {Length:0.##} m"), new Point(hull.Center.X, dimY + 12), foreground, HorizontalAlignment.Center);

        // 7. Depth dimension left of the hull.
        double dimX = hull.Left - 28;
        context.DrawLine(thinPen, new Point(dimX, hull.Top), new Point(dimX, hull.Bottom));
        DrawTick(context, thinPen, new Point(dimX, hull.Top), vertical: false);
        DrawTick(context, thinPen, new Point(dimX, hull.Bottom), vertical: false);
        DrawLabel(context, Format($"D = {Depth:0.##} m"), new Point(dimX - 6, hull.Center.Y), foreground, HorizontalAlignment.Right);
    }

    /// <summary>Short end mark on a dimension line, across the line's direction.</summary>
    private static void DrawTick(DrawingContext context, IPen pen, Point at, bool vertical)
    {
        const double half = 5;
        if (vertical)
        {
            context.DrawLine(pen, new Point(at.X, at.Y - half), new Point(at.X, at.Y + half));
        }
        else
        {
            context.DrawLine(pen, new Point(at.X - half, at.Y), new Point(at.X + half, at.Y));
        }
    }
}
