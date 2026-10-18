using HydroExplorer.Core.Models;

namespace HydroExplorer.Core.Hydrostatics;

/// <summary>
/// Builds the full hydrostatic table for a hull: one row per centimetre of draft,
/// from <see cref="MinDraft"/> up to the hull depth.
/// </summary>
public static class HydrostaticTableGenerator
{
    /// <summary>First draft in the table (m).</summary>
    public const double MinDraft = 0.10;

    /// <summary>Draft step between rows (m).</summary>
    public const double DraftStep = 0.01;

    private const int CentimetresPerMetre = 100;

    /// <summary>Returns all rows for <paramref name="hull"/>, ordered by increasing draft.</summary>
    public static IReadOnlyList<HydrostaticRow> Generate(Hull hull)
    {
        HydrostaticsCalculator.ValidateHull(hull);

        // Work in whole centimetres (integers) to avoid floating-point drift.
        // 0.01 has no exact binary representation, so repeatedly adding it accumulates error:
        // 0.10 + 0.01 added 315 times gives 3.2499999999999747, not 3.25.
        // Counting integer centimetres and dividing once per row avoids that.
        int firstCm = ToWholeCentimetres(MinDraft);
        int lastCm = ToWholeCentimetres(hull.Depth);

        var rows = new List<HydrostaticRow>();
        for (int cm = firstCm; cm <= lastCm; cm++)
        {
            // 325 / 100.0 already gives the double closest to 3.25; Math.Round(…, 2) makes the
            // "exactly 2 decimals" intent explicit and costs nothing.
            double draft = Math.Round((double)cm / CentimetresPerMetre, 2);
            rows.Add(HydrostaticsCalculator.Calculate(hull, draft));
        }

        return rows;
    }

    /// <summary>
    /// Converts metres to whole centimetres, rounding down. The tiny epsilon stops a value
    /// like 4.0 stored as 3.9999999999 from losing a centimetre.
    /// </summary>
    private static int ToWholeCentimetres(double metres) =>
        (int)Math.Floor(metres * CentimetresPerMetre + 1e-9);
}
