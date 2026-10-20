using System.Numerics;

namespace HydroExplorer.App.Rendering;

/// <summary>
/// Decides which faces to draw and in what order: the painter's algorithm, in two groups.
/// </summary>
/// <remarks>
/// <para><b>Painter's algorithm:</b> sort the faces by distance and draw the farthest first, so nearer faces
/// paint over farther ones, like a painter doing the background before the foreground. A GPU would use a depth
/// buffer instead; for about a thousand faces of a convex-ish hull, sorting is simple and good enough.</para>
/// <para><b>Back-face culling</b> first: faces turned away from the camera are hidden by the rest of the closed
/// model, so they are dropped (about half of them).</para>
/// <para><b>Two groups</b> around the water: the view draws <see cref="BelowWater"/>, then the translucent water
/// plane, then <see cref="AboveWater"/>. That's correct because the camera always looks down at the water (it
/// orbits a point on the deck with pitch ≥ 5°), so everything below the waterline is behind the water plane.</para>
/// <para>One instance is reused for every frame: its lists keep their capacity, so drawing allocates nothing.</para>
/// </remarks>
public sealed class DrawOrder
{
    private readonly List<(float Depth, MeshQuad Quad)> _sortBuffer = new();
    private readonly List<MeshQuad> _belowWater = new();
    private readonly List<MeshQuad> _aboveWater = new();

    // Farthest first. A static lambda is created once, not per sort.
    private static readonly Comparison<(float Depth, MeshQuad Quad)> FarthestFirst = (a, b) => b.Depth.CompareTo(a.Depth);

    /// <summary>Visible faces below the waterline, farthest first. Drawn before the water plane.</summary>
    public IReadOnlyList<MeshQuad> BelowWater => _belowWater;

    /// <summary>Visible faces above the waterline (hull, deck, superstructure), farthest first. Drawn after it.</summary>
    public IReadOnlyList<MeshQuad> AboveWater => _aboveWater;

    /// <summary>Recomputes both lists for <paramref name="mesh"/> seen from <paramref name="camera"/>.</summary>
    public void Update(ShipMesh mesh, OrbitCamera camera)
    {
        Vector3 eye = camera.Eye;

        // Depth along the viewing direction (not straight-line distance): what "in front of" means on screen.
        Vector3 forward = Vector3.Normalize(camera.Target - eye);

        Fill(_belowWater, mesh, eye, forward, belowWater: true);
        Fill(_aboveWater, mesh, eye, forward, belowWater: false);
    }

    private void Fill(List<MeshQuad> result, ShipMesh mesh, Vector3 eye, Vector3 forward, bool belowWater)
    {
        _sortBuffer.Clear();
        foreach (MeshQuad quad in mesh.Quads)
        {
            if (quad.IsBelowWater == belowWater && FaceShading.IsFacingCamera(quad, eye))
            {
                _sortBuffer.Add((Vector3.Dot(quad.Centroid - eye, forward), quad));
            }
        }

        _sortBuffer.Sort(FarthestFirst);

        result.Clear();
        foreach ((float _, MeshQuad quad) in _sortBuffer)
        {
            result.Add(quad);
        }
    }
}
