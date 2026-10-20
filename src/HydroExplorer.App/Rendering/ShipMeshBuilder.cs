using System.Numerics;
using HydroExplorer.Core.Hydrostatics;
using HydroExplorer.Core.Models;

namespace HydroExplorer.App.Rendering;

/// <summary>
/// Builds the 3D model of a hull as four-cornered faces: both hull sides, the deck, and for ship hulls a
/// decorative superstructure (deckhouse, bridge, funnel).
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

    /// <summary>Top of the funnel above the deck, as a fraction of the depth D. Bounds every vertex: z ≤ D · (1 + this).</summary>
    public const double SuperstructureHeightFactor = 0.9;

    // Superstructure proportions (decorative; they don't affect the hydrostatics). Lengths are fractions of L
    // from the stern, heights fractions of D above the deck: a typical aft-accommodation coaster or feeder.
    private const double DeckhouseAft = 0.04, DeckhouseFore = 0.20, DeckhouseHeight = 0.5, DeckhouseWidth = 0.8;
    private const double BridgeAft = 0.12, BridgeHeight = 0.25;
    private const double FunnelAft = 0.06, FunnelFore = 0.10, FunnelWidth = 0.25;

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
            AddSuperstructure(quads, hull);
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

    private static void AddSuperstructure(List<MeshQuad> quads, Hull hull)
    {
        double l = hull.Length, d = hull.Depth;

        // The deckhouse is in the aft half, where the deck narrows towards the stern, so its width is set by
        // the narrower (aft) end of its footprint.
        double deckhouseAft = DeckhouseAft * l, deckhouseFore = DeckhouseFore * l;
        double halfWidth = DeckhouseWidth * Math.Min(
            HullGeometry.HalfBreadth(hull, deckhouseAft, d), HullGeometry.HalfBreadth(hull, deckhouseFore, d));
        double deckhouseTop = d + DeckhouseHeight * d;
        AddBox(quads, deckhouseAft, deckhouseFore, halfWidth, d, deckhouseTop, MeshPart.Superstructure);

        // Bridge: the forward part of the deckhouse, one level higher.
        AddBox(quads, BridgeAft * l, deckhouseFore, halfWidth, deckhouseTop, deckhouseTop + BridgeHeight * d, MeshPart.Superstructure);

        // Funnel: a narrow box on the deckhouse roof, aft of the bridge, up to the overall height limit.
        AddBox(quads, FunnelAft * l, FunnelFore * l, FunnelWidth * halfWidth, deckhouseTop, d + SuperstructureHeightFactor * d, MeshPart.Funnel);
    }

    /// <summary>A box standing on <paramref name="bottom"/>, centred on the centreline: four walls and a roof.</summary>
    /// <remarks>No floor: it sits on the deck or on another box, so its underside can never be seen.</remarks>
    private static void AddBox(List<MeshQuad> quads, double aft, double fore, double halfWidth, double bottom, double top, MeshPart part)
    {
        double s = -halfWidth, p = halfWidth;  // starboard and port edges
        quads.Add(new MeshQuad(V(aft, s, bottom), V(fore, s, bottom), V(fore, s, top), V(aft, s, top), part, false));  // starboard wall
        quads.Add(new MeshQuad(V(aft, p, bottom), V(aft, p, top), V(fore, p, top), V(fore, p, bottom), part, false));  // port wall
        quads.Add(new MeshQuad(V(aft, s, bottom), V(aft, s, top), V(aft, p, top), V(aft, p, bottom), part, false));    // aft wall
        quads.Add(new MeshQuad(V(fore, s, bottom), V(fore, p, bottom), V(fore, p, top), V(fore, s, top), part, false)); // fore wall
        quads.Add(new MeshQuad(V(aft, s, top), V(fore, s, top), V(fore, p, top), V(aft, p, top), part, false));        // roof
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
