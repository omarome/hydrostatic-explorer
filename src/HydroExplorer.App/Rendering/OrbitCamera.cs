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

    private float _pitchDegrees = 30;
    private float _distance = 100;

    /// <summary>The point the camera looks at and orbits around.</summary>
    public Vector3 Target { get; set; }

    /// <summary>Angle around the vertical axis. 0 = looking at the starboard side.</summary>
    public float YawDegrees { get; set; }

    /// <summary>Angle above the horizontal, clamped to 5°–85° so the camera never goes under the water or straight overhead.</summary>
    public float PitchDegrees
    {
        get => _pitchDegrees;
        set => _pitchDegrees = Math.Clamp(value, MinPitchDegrees, MaxPitchDegrees);
    }

    /// <summary>Distance from the target to the camera (m). Must be positive.</summary>
    public float Distance
    {
        get => _distance;
        set => _distance = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "Distance must be positive.");
    }

    /// <summary>Size of the drawing area in pixels.</summary>
    public Size Viewport { get; set; }

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

        Vector4 clip = Vector4.Transform(new Vector4(point, 1), ViewProjection());

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
    public float ViewDepth(Vector3 point) => -Vector3.Transform(point, View()).Z;

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

    private float AspectRatio => Viewport.Height > 0 ? (float)(Viewport.Width / Viewport.Height) : 1;

    // Clipping planes scale with the distance, so zooming far out or close in both keep good depth precision.
    private float NearPlane => Distance * 0.01f;

    private float FarPlane => Distance * 10;

    private Matrix4x4 View() => Matrix4x4.CreateLookAt(Eye, Target, Vector3.UnitZ);

    private Matrix4x4 ViewProjection() =>
        View() * Matrix4x4.CreatePerspectiveFieldOfView(Radians(FieldOfViewDegrees), AspectRatio, NearPlane, FarPlane);

    private static float Radians(float degrees) => degrees * MathF.PI / 180;
}
