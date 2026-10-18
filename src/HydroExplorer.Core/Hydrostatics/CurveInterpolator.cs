using HydroExplorer.Core.Models;

namespace HydroExplorer.Core.Hydrostatics;

/// <summary>Reads values off a sampled curve between its points.</summary>
public static class CurveInterpolator
{
    /// <summary>
    /// Displacement at <paramref name="draft"/> by straight-line interpolation between the two nearest
    /// curve points (exact for a box hull, whose displacement is linear in draft). Returns null if the curve
    /// is empty or the draft lies outside it.
    /// </summary>
    /// <param name="curve">Points ordered by increasing draft, as returned by the curve endpoint.</param>
    public static double? InterpolateDisplacement(IReadOnlyList<CurvePoint> curve, double draft)
    {
        if (curve.Count == 0 || draft < curve[0].Draft || draft > curve[^1].Draft)
        {
            return null;
        }

        // Find the first point at or beyond the draft; the segment is (previous, that point).
        for (int i = 0; i < curve.Count; i++)
        {
            CurvePoint upper = curve[i];
            if (upper.Draft < draft)
            {
                continue;
            }

            if (i == 0 || upper.Draft == draft)
            {
                return upper.Displacement;
            }

            CurvePoint lower = curve[i - 1];
            double fraction = (draft - lower.Draft) / (upper.Draft - lower.Draft);
            return lower.Displacement + fraction * (upper.Displacement - lower.Displacement);
        }

        return null;  // unreachable: the range check above guarantees a match
    }
}
