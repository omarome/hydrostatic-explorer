using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Rendering;
using HydroExplorer.App.Rendering;
using HydroExplorer.Core.Models;
using static HydroExplorer.App.Controls.HullDrawing;  // DrawLabel, Caption

namespace HydroExplorer.App.Controls;

/// <summary>
/// An interactive, shaded 3D view of the hull (and, for ships, the superstructure) with the water at
/// <see cref="Draft"/>. Drag to rotate, scroll to zoom, double-click to reset.
/// </summary>
/// <remarks>
/// <para>No 3D engine: the math lives in <c>Rendering/</c> (camera, mesh, culling and sorting, shading, all
/// unit-tested) and this control only draws the resulting 2D polygons with the ordinary DrawingContext,
/// in three passes: hull below the water, the water, everything above it.</para>
/// <para>Like the other hull controls, its inputs are StyledProperties bound from <c>HullViewModel</c>, and its
/// colours are brush properties set from the palette by a style in AppStyles.axaml.</para>
/// <para>It implements <see cref="ICustomHitTest"/> so the whole area takes pointer input, including empty
/// space around the ship (a control with no background is otherwise only "hit" where it has drawn).</para>
/// </remarks>
public sealed class Ship3DView : Control, ICustomHitTest
{
    // ---- Styled properties: the hull and the draft ----
    //
    // WPF note: DependencyProperty.Register in WPF (see HullProfileView for the full comparison).

    public static readonly StyledProperty<double> LengthProperty =
        AvaloniaProperty.Register<Ship3DView, double>(nameof(Length));

    public static readonly StyledProperty<double> BeamProperty =
        AvaloniaProperty.Register<Ship3DView, double>(nameof(Beam));

    public static readonly StyledProperty<double> DepthProperty =
        AvaloniaProperty.Register<Ship3DView, double>(nameof(Depth));

    /// <summary>Null for a box barge.</summary>
    public static readonly StyledProperty<HullShape?> ShapeProperty =
        AvaloniaProperty.Register<Ship3DView, HullShape?>(nameof(Shape));

    /// <summary>Null means "no draft selected": the ship is drawn without water.</summary>
    public static readonly StyledProperty<double?> DraftProperty =
        AvaloniaProperty.Register<Ship3DView, double?>(nameof(Draft));

    // ---- Brushes, set from Palette.axaml by the "controls|Ship3DView" style ----

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<Ship3DView>();

    public static readonly StyledProperty<IBrush?> HintBrushProperty =
        AvaloniaProperty.Register<Ship3DView, IBrush?>(nameof(HintBrush));

    public static readonly StyledProperty<IBrush?> HullTopsideBrushProperty =
        AvaloniaProperty.Register<Ship3DView, IBrush?>(nameof(HullTopsideBrush));

    public static readonly StyledProperty<IBrush?> HullBottomBrushProperty =
        AvaloniaProperty.Register<Ship3DView, IBrush?>(nameof(HullBottomBrush));

    public static readonly StyledProperty<IBrush?> DeckBrushProperty =
        AvaloniaProperty.Register<Ship3DView, IBrush?>(nameof(DeckBrush));

    public static readonly StyledProperty<IBrush?> SuperstructureBrushProperty =
        AvaloniaProperty.Register<Ship3DView, IBrush?>(nameof(SuperstructureBrush));

    public static readonly StyledProperty<IBrush?> FunnelBrushProperty =
        AvaloniaProperty.Register<Ship3DView, IBrush?>(nameof(FunnelBrush));

    public static readonly StyledProperty<IBrush?> GangwayBrushProperty =
        AvaloniaProperty.Register<Ship3DView, IBrush?>(nameof(GangwayBrush));

    public static readonly StyledProperty<IBrush?> HelideckBrushProperty =
        AvaloniaProperty.Register<Ship3DView, IBrush?>(nameof(HelideckBrush));

    public static readonly StyledProperty<IBrush?> EdgeBrushProperty =
        AvaloniaProperty.Register<Ship3DView, IBrush?>(nameof(EdgeBrush));

    public static readonly StyledProperty<IBrush?> WaterplaneBrushProperty =
        AvaloniaProperty.Register<Ship3DView, IBrush?>(nameof(WaterplaneBrush));

    public static readonly StyledProperty<IBrush?> WaterlineBrushProperty =
        AvaloniaProperty.Register<Ship3DView, IBrush?>(nameof(WaterlineBrush));

    static Ship3DView()
    {
        AffectsRender<Ship3DView>(
            LengthProperty, BeamProperty, DepthProperty, ShapeProperty, DraftProperty,
            ForegroundProperty, HintBrushProperty, HullTopsideBrushProperty, HullBottomBrushProperty, DeckBrushProperty,
            SuperstructureBrushProperty, FunnelBrushProperty, GangwayBrushProperty, HelideckBrushProperty, EdgeBrushProperty, WaterplaneBrushProperty, WaterlineBrushProperty);
    }

    public double Length { get => GetValue(LengthProperty); set => SetValue(LengthProperty, value); }

    public double Beam { get => GetValue(BeamProperty); set => SetValue(BeamProperty, value); }

    public double Depth { get => GetValue(DepthProperty); set => SetValue(DepthProperty, value); }

    public HullShape? Shape { get => GetValue(ShapeProperty); set => SetValue(ShapeProperty, value); }

    public double? Draft { get => GetValue(DraftProperty); set => SetValue(DraftProperty, value); }

    /// <summary>Caption text.</summary>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>The "drag to rotate" hint.</summary>
    public IBrush? HintBrush { get => GetValue(HintBrushProperty); set => SetValue(HintBrushProperty, value); }

    /// <summary>Hull above the waterline.</summary>
    public IBrush? HullTopsideBrush { get => GetValue(HullTopsideBrushProperty); set => SetValue(HullTopsideBrushProperty, value); }

    /// <summary>Hull below the waterline (antifouling red).</summary>
    public IBrush? HullBottomBrush { get => GetValue(HullBottomBrushProperty); set => SetValue(HullBottomBrushProperty, value); }

    public IBrush? DeckBrush { get => GetValue(DeckBrushProperty); set => SetValue(DeckBrushProperty, value); }

    public IBrush? SuperstructureBrush { get => GetValue(SuperstructureBrushProperty); set => SetValue(SuperstructureBrushProperty, value); }

    public IBrush? FunnelBrush { get => GetValue(FunnelBrushProperty); set => SetValue(FunnelBrushProperty, value); }

    /// <summary>SOV walk-to-work gangway and its tower.</summary>
    public IBrush? GangwayBrush { get => GetValue(GangwayBrushProperty); set => SetValue(GangwayBrushProperty, value); }

    /// <summary>SOV helicopter deck.</summary>
    public IBrush? HelideckBrush { get => GetValue(HelideckBrushProperty); set => SetValue(HelideckBrushProperty, value); }

    /// <summary>Faint outline of each hull face, which shows the hull's curvature.</summary>
    public IBrush? EdgeBrush { get => GetValue(EdgeBrushProperty); set => SetValue(EdgeBrushProperty, value); }

    /// <summary>Translucent water surface.</summary>
    public IBrush? WaterplaneBrush { get => GetValue(WaterplaneBrushProperty); set => SetValue(WaterplaneBrushProperty, value); }

    /// <summary>The waterline drawn round the hull.</summary>
    public IBrush? WaterlineBrush { get => GetValue(WaterlineBrushProperty); set => SetValue(WaterlineBrushProperty, value); }

    // ---- State ----

    private const float DefaultYawDegrees = 30;    // starboard bow quarter: the side and the bow
    private const float DefaultPitchDegrees = 22;
    private const float DragYawDegreesPerPixel = 0.4f;
    private const float DragPitchDegreesPerPixel = 0.3f;
    private const float ZoomStep = 0.9f;           // one wheel notch: 10 % closer
    private const int BrightnessLevels = 32;       // shading steps; each gets one cached brush

    private const string Hint = "Drag to rotate · scroll to zoom · double-click to reset";

    private readonly OrbitCamera _camera = new();
    private readonly DrawOrder _drawOrder = new();

    // Shaded fill brush and seam pen per (part, below water?, brightness step), so drawing a frame doesn't
    // create a brush per face. Cleared whenever a palette brush changes (e.g. a light/dark theme switch).
    private readonly Dictionary<(MeshPart Part, bool BelowWater, int Level), (IBrush Fill, IPen Seam)> _shadeCache = new();

    // Rebuilt when the hull or the draft changes; the camera is re-framed only when the hull changes.
    private ShipMesh? _mesh;
    private Vector3[] _framingPoints = Array.Empty<Vector3>();
    private Vector3[]? _waterPlane;
    private Vector3[]? _waterline;
    private bool _meshIsStale = true;
    private bool _viewNeedsReset = true;
    private bool _viewWasMoved;  // false until the user rotates or zooms; until then a resize re-frames the view
    private Size _lastViewport;

    private Point? _dragFrom;

    public Ship3DView()
    {
        // Zoomed in, the model is bigger than the control; don't let it paint over the rest of the panel.
        ClipToBounds = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == LengthProperty || change.Property == BeamProperty
            || change.Property == DepthProperty || change.Property == ShapeProperty)
        {
            _meshIsStale = true;
            _viewNeedsReset = true;  // a different hull: frame it afresh
        }
        else if (change.Property == DraftProperty)
        {
            _meshIsStale = true;     // same hull, new waterline: keep the user's view
        }
        else if (change.Property.PropertyType == typeof(IBrush))
        {
            _shadeCache.Clear();
        }
    }

    // ---- Drawing ----

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (!EnsureMesh() || _mesh is not { } mesh || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        _camera.Viewport = Bounds.Size;
        if (_viewNeedsReset || (!_viewWasMoved && Bounds.Size != _lastViewport))
        {
            ResetView();
        }

        _lastViewport = Bounds.Size;

        _drawOrder.Update(mesh, _camera);
        Vector3 towardLight = FaceShading.Headlight(_camera.Eye, _camera.Target);

        // Pass 1: hull below the water. Pass 2: the water over it. Pass 3: everything above the water.
        foreach (MeshQuad quad in _drawOrder.BelowWater)
        {
            DrawFace(context, quad, towardLight);
        }

        DrawWater(context);

        foreach (MeshQuad quad in _drawOrder.AboveWater)
        {
            DrawFace(context, quad, towardLight);
        }

        if (Foreground is { } foreground)
        {
            DrawLabel(context, Caption(ValidDraft()), new Point(Bounds.Width / 2, 12), foreground, HorizontalAlignment.Center);
        }

        if (HintBrush is { } hintBrush)
        {
            DrawLabel(context, Hint, new Point(8, Bounds.Height - 10), hintBrush, HorizontalAlignment.Left);
        }
    }

    private void DrawFace(DrawingContext context, MeshQuad quad, Vector3 towardLight)
    {
        if (_camera.Project(quad.A) is not Point a || _camera.Project(quad.B) is not Point b
            || _camera.Project(quad.C) is not Point c || _camera.Project(quad.D) is not Point d)
        {
            return;  // can't happen within the zoom limits, but never draw half a face
        }

        float brightness = FaceShading.Brightness(FaceShading.Normal(quad), towardLight);
        if (Shade(quad, brightness) is not var (fill, seam))
        {
            return;  // brushes not styled yet
        }

        // The seam pen is the fill colour: it covers the hairline gaps antialiasing leaves between neighbouring faces.
        var polygon = new PolylineGeometry(new[] { a, b, c, d }, isFilled: true);
        context.DrawGeometry(fill, seam, polygon);

        if (quad.Part == MeshPart.Hull && EdgeBrush is { } edgeBrush)
        {
            context.DrawGeometry(null, new Pen(edgeBrush, 0.5), polygon);
        }
    }

    private void DrawWater(DrawingContext context)
    {
        if (_waterPlane is null || _waterline is null)
        {
            return;
        }

        if (WaterplaneBrush is { } waterBrush && ProjectAll(_waterPlane) is { } plane)
        {
            context.DrawGeometry(waterBrush, null, new PolylineGeometry(plane, isFilled: true));
        }

        if (WaterlineBrush is { } lineBrush && ProjectAll(_waterline) is { } line)
        {
            context.DrawGeometry(null, new Pen(lineBrush, 1.5), new PolylineGeometry(line, isFilled: false));
        }
    }

    /// <summary>The palette colour for this face, scaled by its brightness (cached per brightness step).</summary>
    private (IBrush Fill, IPen Seam)? Shade(in MeshQuad quad, float brightness)
    {
        IBrush? baseBrush = quad.Part switch
        {
            MeshPart.Deck => DeckBrush,
            MeshPart.Superstructure => SuperstructureBrush,
            MeshPart.Funnel => FunnelBrush,
            MeshPart.Gangway => GangwayBrush,
            MeshPart.Helideck => HelideckBrush,
            _ => quad.IsBelowWater ? HullBottomBrush : HullTopsideBrush,
        };
        if (baseBrush is not ISolidColorBrush solid)
        {
            return null;
        }

        int level = (int)MathF.Round(brightness * BrightnessLevels);
        var key = (quad.Part, quad.IsBelowWater, level);
        if (!_shadeCache.TryGetValue(key, out var shade))
        {
            float scale = (float)level / BrightnessLevels;
            Color color = solid.Color;
            var shaded = new ImmutableSolidColorBrush(Color.FromArgb(
                color.A, (byte)(color.R * scale), (byte)(color.G * scale), (byte)(color.B * scale)));
            shade = (shaded, new ImmutablePen(shaded, 0.75));
            _shadeCache[key] = shade;
        }

        return shade;
    }

    private Point[]? ProjectAll(Vector3[] points)
    {
        var projected = new Point[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            if (_camera.Project(points[i]) is not Point p)
            {
                return null;
            }

            projected[i] = p;
        }

        return projected;
    }

    // ---- Mesh and camera ----

    /// <summary>The draft if it is a valid waterline for the current hull, else null.</summary>
    /// <remarks>While the hull changes, Draft can briefly still hold the old hull's draft, deeper than the new hull.</remarks>
    private double? ValidDraft() => Draft is double t && t > 0 && t <= Depth ? t : null;

    /// <summary>Rebuilds the mesh if the hull or draft changed. False when there is no hull to draw.</summary>
    private bool EnsureMesh()
    {
        if (!_meshIsStale)
        {
            return _mesh is not null;
        }

        _meshIsStale = false;
        if (!(Length > 0 && Beam > 0 && Depth > 0))
        {
            _mesh = null;
            return false;
        }

        var hull = new Hull(0, string.Empty, Length, Beam, Depth, Shape);
        double? draft = ValidDraft();
        _mesh = ShipMeshBuilder.Build(hull, draft);
        _waterPlane = draft is double t ? ShipMeshBuilder.WaterPlane(hull, t) : null;
        _waterline = draft is double w ? ShipMeshBuilder.Waterline(hull, w) : null;

        // Frame the model plus the widest water there could be (at deck level), so the view doesn't change
        // when the draft does.
        _framingPoints = _mesh.Quads
            .SelectMany(q => new[] { q.A, q.B, q.C, q.D })
            .Concat(ShipMeshBuilder.WaterPlane(hull, Depth))
            .ToArray();
        return true;
    }

    /// <summary>The default view: the starboard bow quarter, aimed at the deck amidships, filling the panel.</summary>
    private void ResetView()
    {
        _camera.YawDegrees = DefaultYawDegrees;
        _camera.PitchDegrees = DefaultPitchDegrees;

        // Aiming at deck level keeps the camera above the water at any pitch (see DrawOrder).
        var target = new Vector3((float)Length / 2, 0, (float)Depth);
        _camera.Frame(_framingPoints, target, Bounds.Size);

        _viewNeedsReset = false;
        _viewWasMoved = false;
    }

    // ---- Interaction (view glue: it only moves this control's camera) ----

    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (e.ClickCount == 2)
        {
            ResetView();
            InvalidateVisual();
        }
        else
        {
            // Capture: keep receiving moves while dragging, even outside the control.
            _dragFrom = e.GetPosition(this);
            e.Pointer.Capture(this);
        }

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragFrom is not Point from)
        {
            return;
        }

        Point to = e.GetPosition(this);
        _camera.YawDegrees -= (float)(to.X - from.X) * DragYawDegreesPerPixel;    // drag right: the ship turns right
        _camera.PitchDegrees += (float)(to.Y - from.Y) * DragPitchDegreesPerPixel; // drag down: look from higher up
        _dragFrom = to;
        _viewWasMoved = true;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragFrom = null;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragFrom = null;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _camera.Zoom(MathF.Pow(ZoomStep, (float)e.Delta.Y));  // wheel up (positive) zooms in
        _viewWasMoved = true;
        InvalidateVisual();
        e.Handled = true;
    }
}
