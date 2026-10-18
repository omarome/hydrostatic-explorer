using Avalonia;

namespace HydroExplorer.App.Controls;

/// <summary>
/// Isometric projection of the box hull: turns a 3D point on the hull (metres) into a 2D screen point
/// (pixels), scaled to fit the control. Pure math with no drawing, so it can be unit-tested.
/// </summary>
/// <remarks>
/// Hull coordinates: x along the length (0..L), y across the beam (0..B), z up from the keel (0..D).
/// In an isometric view the x and y axes are drawn 30° above horizontal, to the right and to the left,
/// and z is drawn straight up, all with the same scale:
/// <code>
///   u = (x − y) · cos 30°        (to the right)
///   v = z + (x + y) · sin 30°    (upwards)
/// </code>
/// We look from above the corner (0, 0, 0), so the visible faces are the deck (z = D), the side y = 0
/// and the end x = 0. Points with a larger x + y are further away.
/// </remarks>
/// <param name="Origin">Screen position of the keel corner (0, 0, 0).</param>
/// <param name="Scale">Pixels per metre, the same along all three axes.</param>
public sealed record HullIsometricLayout(Point Origin, double Scale)
{
    private static readonly double Cos30 = Math.Cos(Math.PI / 6);
    private const double Sin30 = 0.5;

    /// <summary>Room around the drawing for the caption and the dimension labels.</summary>
    public static readonly Thickness Margins = new(left: 80, top: 32, right: 32, bottom: 40);

    /// <summary>Returns null when there is nothing sensible to draw (no hull, or no room).</summary>
    public static HullIsometricLayout? Create(Size available, double length, double beam, double depth)
    {
        if (length <= 0 || beam <= 0 || depth <= 0)
        {
            return null;
        }

        double maxWidth = available.Width - Margins.Left - Margins.Right;
        double maxHeight = available.Height - Margins.Top - Margins.Bottom;
        if (maxWidth <= 0 || maxHeight <= 0)
        {
            return null;
        }

        // Size of the projected box at a scale of 1 px/m. u runs from −B·cos30 (corner (0, B, 0)) to
        // L·cos30 (corner (L, 0, 0)); v runs from 0 (the keel corner) to D + (L + B)·sin30 (the far deck corner).
        double width = (length + beam) * Cos30;
        double height = depth + (length + beam) * Sin30;

        // One scale for every axis, as in the side view, so the box keeps its true proportions.
        double scale = Math.Min(maxWidth / width, maxHeight / height);

        // Centre the drawing in the free space, then find where (0, 0, 0) lands inside it.
        double left = Margins.Left + (maxWidth - width * scale) / 2;
        double bottom = Margins.Top + (maxHeight + height * scale) / 2;
        var origin = new Point(left + beam * Cos30 * scale, bottom);
        return new HullIsometricLayout(origin, scale);
    }

    /// <summary>Screen point of hull point (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) in metres.</summary>
    public Point Project(double x, double y, double z)
    {
        double u = (x - y) * Cos30;
        double v = z + (x + y) * Sin30;

        // Screen y grows downwards, so "up" (positive v) means subtracting.
        return new Point(Origin.X + u * Scale, Origin.Y - v * Scale);
    }
}
