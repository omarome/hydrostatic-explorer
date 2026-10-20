using HydroExplorer.Core.Models;

namespace HydroExplorer.Core.Hydrostatics;

/// <summary>
/// The hull surface: how wide the hull is at any point. Used by the numerical hydrostatics (and later the 3D view).
/// </summary>
/// <remarks>
/// Coordinates: x along the length (0 = stern, L = bow), z up from the keel (0..D). The hull is symmetric about
/// the centreline, so one half-breadth y describes both sides (port at −y, starboard at +y).
/// </remarks>
public static class HullGeometry
{
    /// <summary>
    /// Half-breadth y (m) at position <paramref name="x"/> along the hull and height <paramref name="z"/> above the keel.
    /// For a box it is B/2 everywhere. For a ship hull it is <c>B/2 · f(ξ) · g(z)</c>:
    /// <code>
    ///   ξ = (x − L/2) / (L/2)          −1 at the stern, 0 amidships, +1 at the bow
    ///   f(ξ) = 1 − |ξ|^p               p = bow exponent forward (ξ ≥ 0), stern exponent aft
    ///   g(z) = 1 − (1 − z/D)^q         q = section exponent
    /// </code>
    /// </summary>
    /// <remarks>Expects 0 ≤ x ≤ L and 0 ≤ z ≤ D; not validated, because this runs thousands of times per table row.</remarks>
    public static double HalfBreadth(Hull hull, double x, double z)
    {
        double halfBeam = hull.Beam / 2;
        if (hull.Shape is not HullShape shape)
        {
            return halfBeam;  // box barge
        }

        double halfLength = hull.Length / 2;
        double xi = (x - halfLength) / halfLength;
        double p = xi >= 0 ? shape.BowExponent : shape.SternExponent;
        double waterlineShape = 1 - Math.Pow(Math.Abs(xi), p);                        // f(ξ)
        double sectionShape = 1 - Math.Pow(1 - z / hull.Depth, shape.SectionExponent);  // g(z)

        return halfBeam * waterlineShape * sectionShape;
    }
}
