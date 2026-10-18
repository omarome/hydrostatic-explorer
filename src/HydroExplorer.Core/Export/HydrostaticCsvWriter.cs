using System.Globalization;
using System.Text;
using HydroExplorer.Core.Models;

namespace HydroExplorer.Core.Export;

/// <summary>
/// Turns hydrostatic rows into CSV text. Only text: choosing and writing the file is the app's job.
/// </summary>
/// <remarks>
/// Numbers are always written with InvariantCulture ("3.25", never "3,25"), because a comma decimal
/// separator would split one value into two CSV columns. Headers are ASCII so every spreadsheet
/// program reads them the same way.
/// </remarks>
public static class HydrostaticCsvWriter
{
    /// <summary>The header line: one column per <see cref="HydrostaticRow"/> value, with its unit.</summary>
    public const string Header =
        "draft_m,volume_m3,displacement_t,waterplane_area_m2,tpc_t_per_cm,kb_m,bm_m,km_m,bml_m,mct_tm_per_cm";

    // Up to 6 decimals, trailing zeros dropped: 43726.49999999999 → "43726.5", 4 → "4".
    // That's far more precision than the inputs (whole metres, 1 cm drafts) justify, and it hides
    // floating-point noise in the last digits.
    private const string NumberFormat = "0.######";

    /// <summary>The header line followed by one line per row, each ending in "\r\n" (the CSV standard, RFC 4180).</summary>
    public static string ToCsv(IEnumerable<HydrostaticRow> rows)
    {
        var csv = new StringBuilder();
        csv.Append(Header).Append("\r\n");

        foreach (HydrostaticRow row in rows)
        {
            double[] values =
            {
                row.Draft, row.Volume, row.Displacement, row.WaterplaneArea, row.Tpc,
                row.Kb, row.Bm, row.Km, row.Bml, row.Mct,
            };
            csv.AppendJoin(',', values.Select(Format)).Append("\r\n");
        }

        return csv.ToString();
    }

    private static string Format(double value) => value.ToString(NumberFormat, CultureInfo.InvariantCulture);
}
