using Avalonia;

namespace HydroExplorer.App.Controls;

/// <summary>
/// Where the hull goes on screen: one uniform scale (pixels per metre) so the hull keeps its true
/// proportions, centred in the space left after the margins. Pure math with no drawing, so it can be unit-tested.
/// </summary>
/// <param name="Hull">The hull's side profile in pixels (L wide, D tall).</param>
/// <param name="Scale">Pixels per metre, the same horizontally and vertically.</param>
public sealed record HullProfileLayout(Rect Hull, double Scale)
{
    /// <summary>Room around the hull for labels: depth on the left, length below, "BL"/"WL" on the right.</summary>
    public static readonly Thickness Margins = new(left: 100, top: 32, right: 48, bottom: 56);

    /// <summary>Returns null when there is nothing sensible to draw (no hull, or no room).</summary>
    public static HullProfileLayout? Create(Size available, double length, double depth)
    {
        if (length <= 0 || depth <= 0)
        {
            return null;
        }

        double maxWidth = available.Width - Margins.Left - Margins.Right;
        double maxHeight = available.Height - Margins.Top - Margins.Bottom;
        if (maxWidth <= 0 || maxHeight <= 0)
        {
            return null;
        }

        // The smaller of the two ratios fits both directions. Using one scale for x and y keeps
        // the aspect ratio: a 120 m × 12 m pontoon is drawn exactly 10 times wider than tall.
        double scale = Math.Min(maxWidth / length, maxHeight / depth);
        double width = length * scale;
        double height = depth * scale;

        // Centre in the free space. Screen y grows downwards, so the keel is at the bottom edge.
        double left = Margins.Left + (maxWidth - width) / 2;
        double top = Margins.Top + (maxHeight - height) / 2;
        return new HullProfileLayout(new Rect(left, top, width, height), scale);
    }

    /// <summary>Screen y of the keel (baseline): the bottom of the hull.</summary>
    public double KeelY => Hull.Bottom;

    /// <summary>Screen y of the waterline at <paramref name="draft"/> metres, clamped to the hull.</summary>
    public double WaterlineY(double draft) => Math.Clamp(Hull.Bottom - draft * Scale, Hull.Top, Hull.Bottom);
}
