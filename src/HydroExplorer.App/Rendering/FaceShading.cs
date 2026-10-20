using System.Numerics;

namespace HydroExplorer.App.Rendering;

/// <summary>
/// Flat (per-face) shading: how bright a face is, and whether it faces the camera at all.
/// </summary>
/// <remarks>
/// Lambert's cosine law: a surface facing the light gets all of it, one turned by angle θ gets cos θ of it,
/// and one facing away gets none. A constant ambient term keeps the unlit side readable instead of black:
/// <c>brightness = ambient + diffuse · max(0, n·l)</c>. The 3D view multiplies a palette colour by this,
/// so no colours live here.
/// </remarks>
public static class FaceShading
{
    /// <summary>Brightness of a face that gets no direct light.</summary>
    public const float Ambient = 0.35f;

    /// <summary>Extra brightness of a face pointing straight at the light (so the maximum is 1).</summary>
    public const float Diffuse = 0.65f;

    /// <summary>Default direction towards the light: high above, slightly aft and to starboard.</summary>
    public static readonly Vector3 DefaultTowardLight = Vector3.Normalize(new Vector3(-0.3f, -0.5f, 0.8f));

    /// <summary>
    /// Unit normal of <paramref name="quad"/>, or zero if the face has no area (sides meeting at the bow, say).
    /// </summary>
    /// <remarks>
    /// The cross product of the two diagonals works even when the four corners are not quite in one plane,
    /// as on the curved hull. For anticlockwise corners it points towards the viewer (right-hand rule).
    /// </remarks>
    public static Vector3 Normal(in MeshQuad quad)
    {
        Vector3 cross = Vector3.Cross(quad.C - quad.A, quad.D - quad.B);
        float lengthSquared = cross.LengthSquared();
        return lengthSquared < 1e-12f ? Vector3.Zero : cross / MathF.Sqrt(lengthSquared);
    }

    /// <summary>
    /// Back-face culling: true when the outside of the face is turned towards <paramref name="eye"/>.
    /// Faces turned away are hidden behind the rest of a closed model, so they needn't be drawn.
    /// </summary>
    public static bool IsFacingCamera(in MeshQuad quad, Vector3 eye) =>
        Vector3.Dot(Normal(quad), eye - quad.Centroid) > 0;

    /// <summary>Lambert brightness in [Ambient, Ambient + Diffuse] for unit vectors <paramref name="normal"/> and <paramref name="towardLight"/>.</summary>
    public static float Brightness(Vector3 normal, Vector3 towardLight) =>
        Ambient + Diffuse * MathF.Max(0, Vector3.Dot(normal, towardLight));
}
