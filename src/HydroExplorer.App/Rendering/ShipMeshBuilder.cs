using System.Numerics;
using HydroExplorer.Core.Hydrostatics;
using HydroExplorer.Core.Models;

namespace HydroExplorer.App.Rendering;

/// <summary>
/// Builds the 3D model of a hull as four-cornered faces: both hull sides, the deck, and for ship hulls the
/// decorative superstructure from <see cref="SuperstructureBoxes"/>.
/// </summary>
/// <remarks>
/// The hull sides are a grid: <see cref="Segments"/> slices along the length × <see cref="HeightLevels"/> slices
/// up the side, plus one extra row of vertices exactly at the draft, so every face is either wholly below or
/// wholly above the water and can be coloured accordingly. The widths come from
/// <see cref="HullGeometry.HalfBreadth"/>, the same function the hydrostatics integrate, so the picture and the
/// numbers describe the same hull.
/// </remarks>
public static class ShipMeshBuilder
{
    /// <summary>Slices along the length.</summary>
    public const int Segments = 40;

    /// <summary>Slices from keel to deck (before the waterline row is added).</summary>
    public const int HeightLevels = 12;

    /// <summary>Builds the mesh for <paramref name="hull"/> with the waterline at <paramref name="draft"/> (null: no waterline).</summary>
    /// <exception cref="ArgumentOutOfRangeException">The draft is not in (0, D].</exception>
    public static ShipMesh Build(Hull hull, double? draft)
    {
        if (draft is double t && (!(t > 0) || t > hull.Depth))
        {
            throw new ArgumentOutOfRangeException(nameof(draft), draft, $"Draft must be greater than 0 and at most the hull depth ({hull.Depth} m).");
        }

        var quads = new List<MeshQuad>();
        (double[] levels, int waterlineIndex) = HeightLevelsWithWaterline(hull.Depth, draft);

        AddHullSides(quads, hull, levels, waterlineIndex);
        AddDeck(quads, hull);
        if (hull.Shape is null)
        {
            AddBoxBottomAndEnds(quads, hull, levels, waterlineIndex);
        }
        else
        {
            // A ship's sides already close the hull: the half-breadth is 0 at the keel and at both ends.
            foreach (SuperstructureBox box in SuperstructureBoxes.For(hull))
            {
                AddBox(quads, box);
            }
        }

        return new ShipMesh(quads, Bound(quads, Vector3.Min), Bound(quads, Vector3.Max));
    }

    /// <summary>
    /// Evenly spaced heights from keel to deck, with the draft inserted (or an existing level moved onto it).
    /// Returns the index of the waterline level, or −1 without a draft. Faces up to that level are below water.
    /// </summary>
    private static (double[] Levels, int WaterlineIndex) HeightLevelsWithWaterline(double depth, double? draft)
    {
        var levels = new List<double>();
        for (int k = 0; k <= HeightLevels; k++)
        {
            levels.Add(depth * k / HeightLevels);  // from the index, no drift; the last is exactly D
        }

        if (draft is not double t)
        {
            return (levels.ToArray(), -1);
        }

        // If a level is within 1 % of a slice of the draft, move it onto the draft instead of adding a sliver face.
        double snapDistance = depth / HeightLevels * 0.01;
        int nearest = levels.FindIndex(z => Math.Abs(z - t) < snapDistance);
        if (nearest >= 0)
        {
            levels[nearest] = t;
            return (levels.ToArray(), nearest);
        }

        int above = levels.FindIndex(z => z > t);  // exists: t ≤ D and the top level is D
        levels.Insert(above, t);
        return (levels.ToArray(), above);
    }

    private static void AddHullSides(List<MeshQuad> quads, Hull hull, double[] levels, int waterlineIndex)
    {
        for (int i = 0; i < Segments; i++)
        {
            double x0 = hull.Length * i / Segments;
            double x1 = hull.Length * (i + 1) / Segments;
            for (int j = 0; j < levels.Length - 1; j++)
            {
                double z0 = levels[j], z1 = levels[j + 1];
                bool below = j + 1 <= waterlineIndex;
                double h00 = HullGeometry.HalfBreadth(hull, x0, z0), h10 = HullGeometry.HalfBreadth(hull, x1, z0);
                double h01 = HullGeometry.HalfBreadth(hull, x0, z1), h11 = HullGeometry.HalfBreadth(hull, x1, z1);

                // Starboard (−y), anticlockwise seen from starboard: aft-low, fore-low, fore-high, aft-high.
                quads.Add(new MeshQuad(V(x0, -h00, z0), V(x1, -h10, z0), V(x1, -h11, z1), V(x0, -h01, z1), MeshPart.Hull, below));

                // Port (+y): the same corners mirrored, in the opposite order so the normal points to port.
                quads.Add(new MeshQuad(V(x0, h00, z0), V(x0, h01, z1), V(x1, h11, z1), V(x1, h10, z0), MeshPart.Hull, below));
            }
        }
    }

    private static void AddDeck(List<MeshQuad> quads, Hull hull)
    {
        double d = hull.Depth;
        for (int i = 0; i < Segments; i++)
        {
            double x0 = hull.Length * i / Segments;
            double x1 = hull.Length * (i + 1) / Segments;
            double h0 = HullGeometry.HalfBreadth(hull, x0, d), h1 = HullGeometry.HalfBreadth(hull, x1, d);

            // Anticlockwise seen from above. The deck is never "below water", even with the deck awash at T = D.
            quads.Add(new MeshQuad(V(x0, -h0, d), V(x1, -h1, d), V(x1, h1, d), V(x0, h0, d), MeshPart.Deck, IsBelowWater: false));
        }
    }

    private static void AddBoxBottomAndEnds(List<MeshQuad> quads, Hull hull, double[] levels, int waterlineIndex)
    {
        double l = hull.Length, h = hull.Beam / 2;
        bool hasWaterline = waterlineIndex >= 0;

        // Bottom (normal down), one strip per segment like the sides, so depth sorting treats it alike.
        for (int i = 0; i < Segments; i++)
        {
            double x0 = l * i / Segments, x1 = l * (i + 1) / Segments;
            quads.Add(new MeshQuad(V(x0, -h, 0), V(x0, h, 0), V(x1, h, 0), V(x1, -h, 0), MeshPart.Hull, hasWaterline));
        }

        // Transom (x = 0, normal aft) and bow (x = L, normal forward), split at the same levels as the sides.
        for (int j = 0; j < levels.Length - 1; j++)
        {
            double z0 = levels[j], z1 = levels[j + 1];
            bool below = j + 1 <= waterlineIndex;
            quads.Add(new MeshQuad(V(0, -h, z0), V(0, -h, z1), V(0, h, z1), V(0, h, z0), MeshPart.Hull, below));
            quads.Add(new MeshQuad(V(l, -h, z0), V(l, h, z0), V(l, h, z1), V(l, -h, z1), MeshPart.Hull, below));
        }
    }

    /// <summary>A superstructure box as faces: four walls and a roof.</summary>
    /// <remarks>No floor: it sits on the deck or on another box, so its underside can never be seen.</remarks>
    private static void AddBox(List<MeshQuad> quads, SuperstructureBox box)
    {
        (double aft, double fore, double bottom, double top, MeshPart part) = (box.Aft, box.Fore, box.Bottom, box.Top, box.Part);
        double s = -box.HalfWidth, p = box.HalfWidth;  // starboard and port edges
        quads.Add(new MeshQuad(V(aft, s, bottom), V(fore, s, bottom), V(fore, s, top), V(aft, s, top), part, false));  // starboard wall
        quads.Add(new MeshQuad(V(aft, p, bottom), V(aft, p, top), V(fore, p, top), V(fore, p, bottom), part, false));  // port wall
        quads.Add(new MeshQuad(V(aft, s, bottom), V(aft, s, top), V(aft, p, top), V(aft, p, bottom), part, false));    // aft wall
        quads.Add(new MeshQuad(V(fore, s, bottom), V(fore, p, bottom), V(fore, p, top), V(fore, s, top), part, false)); // fore wall
        quads.Add(new MeshQuad(V(aft, s, top), V(fore, s, top), V(fore, p, top), V(aft, p, top), part, false));        // roof
    }

    /// <summary>How far the drawn water reaches past the hull on every side, as a fraction of L.</summary>
    public const double WaterMargin = 0.1;

    /// <summary>
    /// Corners of the water surface at <paramref name="draft"/>: a rectangle around the hull, reaching
    /// <see cref="WaterMargin"/> · L past it, anticlockwise seen from above.
    /// </summary>
    public static Vector3[] WaterPlane(Hull hull, double draft)
    {
        double margin = WaterMargin * hull.Length;
        double aft = -margin, fore = hull.Length + margin, side = hull.Beam / 2 + margin;
        return new[] { V(aft, -side, draft), V(fore, -side, draft), V(fore, side, draft), V(aft, side, draft) };
    }

    /// <summary>
    /// The waterline on the hull at <paramref name="draft"/>: a closed loop up the starboard side and back down
    /// the port side, one point per station. For a box it's the rectangle L × B.
    /// </summary>
    public static Vector3[] Waterline(Hull hull, double draft)
    {
        var loop = new Vector3[2 * (Segments + 1)];
        for (int i = 0; i <= Segments; i++)
        {
            double x = hull.Length * i / Segments;
            double y = HullGeometry.HalfBreadth(hull, x, draft);
            loop[i] = V(x, -y, draft);                    // starboard, stern → bow
            loop[loop.Length - 1 - i] = V(x, y, draft);   // port, bow → stern
        }

        return loop;
    }

    private static Vector3 V(double x, double y, double z) => new((float)x, (float)y, (float)z);

    private static Vector3 Bound(List<MeshQuad> quads, Func<Vector3, Vector3, Vector3> pick)
    {
        Vector3 result = quads[0].A;
        foreach (MeshQuad q in quads)
        {
            result = pick(pick(pick(pick(result, q.A), q.B), q.C), q.D);
        }

        return result;
    }
}
