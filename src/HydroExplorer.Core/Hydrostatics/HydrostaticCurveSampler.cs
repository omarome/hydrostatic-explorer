using HydroExplorer.Core.Models;

namespace HydroExplorer.Core.Hydrostatics;

/// <summary>
/// Samples the displacement curve at evenly spaced drafts, so a chart can draw the whole curve
/// without downloading every row of the table.
/// </summary>
public static class HydrostaticCurveSampler
{
    /// <summary>
    /// Returns <paramref name="points"/> points from <see cref="HydrostaticTableGenerator.MinDraft"/>
    /// up to the hull depth, both ends included.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="points"/> is less than 2.</exception>
    public static IReadOnlyList<CurvePoint> Sample(Hull hull, int points)
    {
        HydrostaticsCalculator.ValidateHull(hull);
        if (points < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(points), points, "A curve needs at least 2 points.");
        }

        double firstDraft = HydrostaticTableGenerator.MinDraft;
        double lastDraft = hull.Depth;
        double step = (lastDraft - firstDraft) / (points - 1);

        var curve = new List<CurvePoint>(points);
        for (int i = 0; i < points; i++)
        {
            // Draft from the index (not by adding step repeatedly), and pin the last point to exactly the depth.
            double draft = i == points - 1 ? lastDraft : firstDraft + i * step;
            HydrostaticRow row = HydrostaticsCalculator.Calculate(hull, draft);
            curve.Add(new CurvePoint(row.Draft, row.Displacement));
        }

        return curve;
    }
}
