using System.Numerics;
using Avalonia;

namespace HydroExplorer.App.Rendering;

/// <summary>
/// A camera that orbits a target point: turn it around (yaw), tilt it up and down (pitch), move it closer or
/// farther (distance). Turns 3D points in hull coordinates into 2D pixels with a perspective projection.
/// </summary>
/// <remarks>
/// <para>World coordinates are the hull's: x towards the bow, y to port, z up. At yaw 0 the camera looks at the
/// starboard side, so the bow is to the right; positive yaw turns the camera anticlockwise seen from above.</para>
/// <para>The usual 3D pipeline: <b>view</b> matrix (world → camera space), then <b>projection</b> matrix (camera
/// space → clip space, where dividing by w makes far things smaller), then map −1..1 to pixels.
/// System.Numerics uses row vectors, so transforms combine left to right: <c>view * projection</c>.</para>
/// </remarks>
public sealed class OrbitCamera
{
    public const float MinPitchDegrees = 5;
    public const float MaxPitchDegrees = 85;

    /// <summary>Vertical field of view. 35° is a mild, natural-looking perspective.</summary>
    public const float FieldOfViewDegrees = 35;

    // The fitted distance leaves this much room around the bounding sphere.
    private const float FitMargin = 1.05f;

    // Zoom limits, as multiples of the framed model's radius around the target: never inside the model
    // (so nothing can end up behind the camera), and never so far that it shrinks to a dot.
    private const float MinDistanceFactor = 1.1f;
    private const float MaxDistanceFactor = 8f;

    private Vector3 _target;
    private float _yawDegrees;
    private Size _viewport;

    // The view and projection matrices are built once and reused for every point, until a property changes:
    // projecting a mesh means thousands of points per frame, but the camera only moves between frames.
    private bool _matricesAreStale = true;
    private Matrix4x4 _view;
    private Matrix4x4 _viewProjection;

    private float _minDistance = 0.01f;
    private float _maxDistance = float.MaxValue;

    private float _pitchDegrees = 30;
    private float _distance = 100;

    /// <summary>The point the camera looks at and orbits around.</summary>
    public Vector3 Target
    {
        get => _target;
        set { _target = value; _matricesAreStale = true; }
    }

    /// <summary>Angle around the vertical axis. 0 = looking at the starboard side.</summary>
    public float YawDegrees
    {
        get => _yawDegrees;
        set { _yawDegrees = value; _matricesAreStale = true; }
    }

    /// <summary>Angle above the horizontal, clamped to 5°–85° so the camera never goes under the water or straight overhead.</summary>
    public float PitchDegrees
    {
        get => _pitchDegrees;
        set { _pitchDegrees = Math.Clamp(value, MinPitchDegrees, MaxPitchDegrees); _matricesAreStale = true; }
    }

    /// <summary>Distance from the target to the camera (m). Must be positive.</summary>
    public float Distance
    {
        get => _distance;
        set
        {
            _distance = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "Distance must be positive.");
            _matricesAreStale = true;
        }
    }

    /// <summary>Size of the drawing area in pixels.</summary>
    public Size Viewport
    {
        get => _viewport;
        set { _viewport = value; _matricesAreStale = true; }
    }

    /// <summary>Camera position: <see cref="Distance"/> away from the target, in the direction set by yaw and pitch.</summary>
    public Vector3 Eye
    {
        get
        {
            float yaw = Radians(YawDegrees);
            float pitch = Radians(PitchDegrees);

            // Unit vector from the target to the camera. Yaw 0 → (0, −1, 0): out on the starboard side.
            var direction = new Vector3(
                MathF.Cos(pitch) * MathF.Sin(yaw),
                -MathF.Cos(pitch) * MathF.Cos(yaw),
                MathF.Sin(pitch));
            return Target + Distance * direction;
        }
    }

    /// <summary>
    /// Pixel position of <paramref name="point"/>, or null if it is behind the camera (or the viewport is empty).
    /// </summary>
    public Point? Project(Vector3 point)
    {
        if (Viewport.Width <= 0 || Viewport.Height <= 0)
        {
            return null;
        }

        UpdateMatrices();
        Vector4 clip = Vector4.Transform(new Vector4(point, 1), _viewProjection);

        // For a perspective projection w is the distance in front of the camera. Too small (or negative)
        // means at or behind the camera, where dividing by w would flip or explode the point.
        if (clip.W <= NearPlane)
        {
            return null;
        }

        // Normalised device coordinates: −1..1 left to right and bottom to top. Screen y grows downwards.
        float x = clip.X / clip.W;
        float y = clip.Y / clip.W;
        return new Point((x + 1) / 2 * Viewport.Width, (1 - y) / 2 * Viewport.Height);
    }

    /// <summary>
    /// How far in front of the camera <paramref name="point"/> is, along the viewing direction.
    /// Larger is farther; the painter's algorithm draws the largest first.
    /// </summary>
    public float ViewDepth(Vector3 point)
    {
        UpdateMatrices();
        return -Vector3.Transform(point, _view).Z;
    }

    /// <summary>
    /// Aims at the centre of the box <paramref name="min"/>–<paramref name="max"/> and backs off until the
    /// box's bounding sphere fits the viewport. A sphere looks the same from every direction, so the model
    /// stays in view however the camera is turned afterwards.
    /// </summary>
    public void FitTo(Vector3 min, Vector3 max, Size viewport)
    {
        Viewport = viewport;
        Target = (min + max) / 2;
        float radius = Vector3.Distance(min, max) / 2;

        // The narrower of the two viewing angles limits the fit. Horizontal half-angle from the vertical one:
        // tan(h) = tan(v) · width / height.
        float halfVertical = Radians(FieldOfViewDegrees) / 2;
        float halfHorizontal = MathF.Atan(MathF.Tan(halfVertical) * AspectRatio);
        float halfAngle = MathF.Min(halfVertical, halfHorizontal);

        // A sphere of radius r seen from distance d fills a half-angle of asin(r / d).
        Distance = MathF.Max(radius, 0.01f) / MathF.Sin(halfAngle) * FitMargin;
    }

    /// <summary>
    /// The default view: aims at <paramref name="target"/> and sets the distance so <paramref name="points"/> fill
    /// about <paramref name="fill"/> of the viewport at the current yaw and pitch. Also sets the zoom limits.
    /// </summary>
    /// <remarks>
    /// Tighter than <see cref="FitTo"/>: a long, narrow ship fills the panel instead of half of it. It starts from
    /// the safe sphere fit, then corrects the distance a few times, because in a perspective view an object's size
    /// on screen is roughly proportional to 1 / distance.
    /// </remarks>
    public void Frame(IReadOnlyCollection<Vector3> points, Vector3 target, Size viewport, float fill = 0.9f)
    {
        Viewport = viewport;
        Target = target;
        float radius = MathF.Max(points.Max(p => Vector3.Distance(p, target)), 0.01f);
        _minDistance = radius * MinDistanceFactor;
        _maxDistance = radius * MaxDistanceFactor;

        float halfVertical = Radians(FieldOfViewDegrees) / 2;
        float halfAngle = MathF.Min(halfVertical, MathF.Atan(MathF.Tan(halfVertical) * AspectRatio));
        Distance = Math.Clamp(radius / MathF.Sin(halfAngle), _minDistance, _maxDistance);

        for (int i = 0; i < 4; i++)
        {
            float reach = Reach(points);
            if (reach <= 0)
            {
                return;  // empty viewport: keep the sphere fit
            }

            Distance = Math.Clamp(Distance * reach / fill, _minDistance, _maxDistance);
        }

        // The 1 / distance rule is approximate for points near the camera, so back off until everything is inside.
        while (Reach(points) > 1 && Distance < _maxDistance)
        {
            Distance = MathF.Min(Distance * 1.02f, _maxDistance);
        }
    }

    /// <summary>
    /// Multiplies the distance by <paramref name="factor"/> (less than 1 zooms in), within the limits set by
    /// <see cref="Frame"/>.
    /// </summary>
    public void Zoom(float factor) => Distance = Math.Clamp(Distance * factor, _minDistance, _maxDistance);

    /// <summary>
    /// How far the farthest projected point reaches from the viewport centre towards the nearer edge:
    /// 1 = touching an edge, more = outside. A point behind the camera counts as far outside.
    /// </summary>
    private float Reach(IEnumerable<Vector3> points)
    {
        double halfWidth = Viewport.Width / 2, halfHeight = Viewport.Height / 2;
        if (halfWidth <= 0 || halfHeight <= 0)
        {
            return 0;
        }

        double reach = 0;
        foreach (Vector3 point in points)
        {
            if (Project(point) is not Point p)
            {
                return float.MaxValue;
            }

            reach = Math.Max(reach, Math.Max(Math.Abs(p.X - halfWidth) / halfWidth, Math.Abs(p.Y - halfHeight) / halfHeight));
        }

        return (float)reach;
    }

    private float AspectRatio => Viewport.Height > 0 ? (float)(Viewport.Width / Viewport.Height) : 1;

    // Clipping planes scale with the distance, so zooming far out or close in both keep good depth precision.
    private float NearPlane => Distance * 0.01f;

    private float FarPlane => Distance * 10;

    private void UpdateMatrices()
    {
        if (!_matricesAreStale)
        {
            return;
        }

        _view = Matrix4x4.CreateLookAt(Eye, Target, Vector3.UnitZ);
        _viewProjection = _view * Matrix4x4.CreatePerspectiveFieldOfView(Radians(FieldOfViewDegrees), AspectRatio, NearPlane, FarPlane);
        _matricesAreStale = false;
    }

    private static float Radians(float degrees) => degrees * MathF.PI / 180;
}
