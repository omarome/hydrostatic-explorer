using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;

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

    /// <summary>
    /// Reuses the inherited text Foreground, so lines and labels follow the light/dark theme.
    /// WPF note: the same idea as TextElement.ForegroundProperty.AddOwner in WPF.
    /// </summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<HullProfileView>();

    static HullProfileView()
    {
        // Redraw (call Render again) whenever any of these properties change.
        // WPF note: FrameworkPropertyMetadataOptions.AffectsRender does this in WPF.
        AffectsRender<HullProfileView>(LengthProperty, DepthProperty, DraftProperty, ForegroundProperty);
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

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    // ---- Drawing ----

    private const double LabelFontSize = 12;
    private const double LineOverhang = 16;  // how far waterline/baseline extend past the hull ends (px)
    private const double LabelOffset = 8;    // vertical gap between a line and its "WL"/"BL" label (px)

    private static readonly IBrush SeaBrush = new ImmutableSolidColorBrush(Color.Parse("#331E88E5"));
    private static readonly IBrush SubmergedBrush = new ImmutableSolidColorBrush(Color.Parse("#991E88E5"));
    private static readonly IPen WaterlinePen = new ImmutablePen(Color.Parse("#1E88E5").ToUInt32(), 2);

    // WPF note: WPF's equivalent is OnRender(DrawingContext). Both record drawing commands that the
    // framework replays; neither draws directly to the screen while the method runs.
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // Bounds.Size is the control's current size, so a resize naturally gives a new layout.
        HullProfileLayout? layout = HullProfileLayout.Create(Bounds.Size, Length, Depth);
        if (layout is null)
        {
            return;
        }

        IBrush foreground = Foreground ?? Brushes.Gray;
        var outlinePen = new Pen(foreground, 2);
        var thinPen = new Pen(foreground, 1);
        var dashedPen = new Pen(foreground, 1, new DashStyle(new double[] { 4, 3 }, 0));
        Rect hull = layout.Hull;
        double? draft = Draft;
        double? waterlineY = draft is null ? null : layout.WaterlineY(draft.Value);

        // 1. Water: a light band across the whole width, darker where it fills the hull.
        if (waterlineY is double seaTop)
        {
            double seaBottom = Math.Min(hull.Bottom + 12, Bounds.Height);
            context.FillRectangle(SeaBrush, new Rect(0, seaTop, Bounds.Width, seaBottom - seaTop));
            context.FillRectangle(SubmergedBrush, new Rect(hull.Left, seaTop, hull.Width, hull.Bottom - seaTop));
        }

        // 2. Hull outline.
        context.DrawRectangle(null, outlinePen, hull);

        // 3. Keel baseline (BL), extended past both ends. "BL" sits just below its line and "WL" just
        //    above its line, so the two labels never overlap at shallow drafts.
        double keelY = layout.KeelY;
        context.DrawLine(dashedPen, new Point(hull.Left - LineOverhang, keelY), new Point(hull.Right + LineOverhang, keelY));
        DrawLabel(context, "BL", new Point(hull.Right + LineOverhang + 4, keelY + LabelOffset), foreground, HorizontalAlignment.Left);

        // 4. Waterline (WL).
        if (waterlineY is double wl)
        {
            context.DrawLine(WaterlinePen, new Point(hull.Left - LineOverhang, wl), new Point(hull.Right + LineOverhang, wl));
            DrawLabel(context, "WL", new Point(hull.Right + LineOverhang + 4, wl - LabelOffset), foreground, HorizontalAlignment.Left);
        }

        // 5. Caption above the hull: the current draft, or a hint. Above the deck it can't collide with
        //    the drawing, even when the hull is only a few pixels tall.
        string caption = draft is null
            ? "Select a draft to show the waterline"
            : Format($"Waterline at T = {draft:F2} m");
        DrawLabel(context, caption, new Point(hull.Center.X, Math.Max(hull.Top - 14, 8)), foreground, HorizontalAlignment.Center);

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

    /// <summary>Draws <paramref name="text"/> vertically centred on <paramref name="anchor"/>.</summary>
    private static void DrawLabel(DrawingContext context, string text, Point anchor, IBrush brush, HorizontalAlignment alignment)
    {
        var formatted = new FormattedText(
            text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, LabelFontSize, brush);

        double x = alignment switch
        {
            HorizontalAlignment.Center => anchor.X - formatted.Width / 2,
            HorizontalAlignment.Right => anchor.X - formatted.Width,
            _ => anchor.X,
        };
        context.DrawText(formatted, new Point(x, anchor.Y - formatted.Height / 2));
    }

    private static string Format(FormattableString text) => text.ToString(CultureInfo.CurrentCulture);
}
