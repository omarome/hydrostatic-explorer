using System.Numerics;

namespace HydroExplorer.App.Rendering;

/// <summary>Which part of the ship a face belongs to; the 3D view picks its colour from this.</summary>
public enum MeshPart
{
    Hull,
    Deck,
    Superstructure,
    Funnel,
}

/// <summary>
/// One four-cornered face of the 3D model, in hull coordinates (metres): x along the length (0 = stern),
/// y across (0 = centreline, starboard negative), z up from the keel.
/// </summary>
/// <remarks>
/// The corners go anticlockwise when seen from outside the ship, so the normal (see <see cref="FaceShading.Normal"/>)
/// points outwards. A "readonly record struct" is a small immutable value type: copied like a number, no heap
/// allocation per face, which matters when a thousand faces are sorted every frame.
/// </remarks>
/// <param name="IsBelowWater">True for hull faces wholly at or below the current waterline.</param>
public readonly record struct MeshQuad(Vector3 A, Vector3 B, Vector3 C, Vector3 D, MeshPart Part, bool IsBelowWater)
{
    /// <summary>The average of the four corners: where the face is, for depth sorting and culling.</summary>
    public Vector3 Centroid => (A + B + C + D) / 4;
}

/// <summary>The faces of the model and the box that contains them all.</summary>
public sealed record ShipMesh(IReadOnlyList<MeshQuad> Quads, Vector3 Min, Vector3 Max);
