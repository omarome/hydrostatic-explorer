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
    /// The table draft nearest to <paramref name="draft"/>: rounded to whole centimetres and kept within
    /// [<see cref="MinDraft"/>, hull depth]. Lets a chart click or slider position (e.g. 3.2547 m) pick
    /// a row that exists (3.25 m). The result is bit-for-bit equal to that row's <see cref="HydrostaticRow.Draft"/>.
    /// </summary>
    public static double SnapToTableDraft(Hull hull, double draft)
    {
        HydrostaticsCalculator.ValidateHull(hull);
        if (!double.IsFinite(draft))
        {
            throw new ArgumentOutOfRangeException(nameof(draft), draft, "Draft must be a finite number.");
        }

        // Clamp in metres first, so huge values can't overflow the int conversion.
        double clamped = Math.Clamp(draft, MinDraft, hull.Depth);
        int cm = Math.Clamp(
            (int)Math.Round(clamped * CentimetresPerMetre),
            ToWholeCentimetres(MinDraft),
            ToWholeCentimetres(hull.Depth));

        // Same expression as in Generate, so the double is identical to the row's draft.
        return Math.Round((double)cm / CentimetresPerMetre, 2);
    }

    /// <summary>
    /// Converts metres to whole centimetres, rounding down. The tiny epsilon stops a value
    /// like 4.0 stored as 3.9999999999 from losing a centimetre.
    /// </summary>
    private static int ToWholeCentimetres(double metres) =>
        (int)Math.Floor(metres * CentimetresPerMetre + 1e-9);
}
