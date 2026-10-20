using Avalonia;
using HydroExplorer.Core.Hydrostatics;
using HydroExplorer.Core.Models;

namespace HydroExplorer.App.Controls;

/// <summary>
/// One cross-section of the hull for the body plan: its half-breadths from the keel (on the centreline) up to
/// the deck, in metres. Aft sections have negative y (drawn left of the centreline), forward ones positive.
/// </summary>
/// <param name="Station">Station number, 0 = stern to 10 = bow.</param>
/// <param name="X">Position of the station along the hull (m from the stern).</param>
public sealed record BodyPlanSection(int Station, double X, bool IsForward, IReadOnlyList<(double Y, double Z)> Points);

/// <summary>
/// The math behind the body plan: which sections to draw, and where they go on screen. Pure, so it can be tested.
/// </summary>
/// <remarks>
/// A body plan is the classic naval-architecture view of a hull's shape: the cross-sections at evenly spaced
/// stations, seen from ahead, overlaid on one centreline. By convention the aft half's sections are drawn on
/// the left and the forward half's on the right, so one drawing shows both ends; midships appears on both.
/// </remarks>
/// <param name="Origin">Screen position of the keel on the centreline.</param>
/// <param name="Scale">Pixels per metre, the same across and up, so sections keep their true shape.</param>
public sealed record BodyPlanLayout(Point Origin, double Scale)
{
    /// <summary>Stations along the length: 10 intervals, the classic body-plan spacing.</summary>
    public const int StationIntervals = 10;

    /// <summary>Points per section from keel to deck; enough for the curved bilge to look smooth.</summary>
    public const int PointsPerSection = 24;

    /// <summary>Room around the drawing: caption and "CL"/"Aft"/"Fore" labels above, the beam label below.</summary>
    public static readonly Thickness Margins = new(left: 56, top: 52, right: 56, bottom: 48);

    /// <summary>Returns null when there is nothing sensible to draw (no hull, or no room).</summary>
    public static BodyPlanLayout? Create(Size available, double beam, double depth)
    {
        if (beam <= 0 || depth <= 0)
        {
            return null;
        }

        double maxWidth = available.Width - Margins.Left - Margins.Right;
        double maxHeight = available.Height - Margins.Top - Margins.Bottom;
        if (maxWidth <= 0 || maxHeight <= 0)
        {
            return null;
        }

        // One scale for both directions (like the side view), centred in the free space.
        double scale = Math.Min(maxWidth / beam, maxHeight / depth);
        double centreX = Margins.Left + maxWidth / 2;
        double keelY = Margins.Top + (maxHeight + depth * scale) / 2;
        return new BodyPlanLayout(new Point(centreX, keelY), scale);
    }

    /// <summary>Screen point of half-breadth <paramref name="y"/> (negative = left) at height <paramref name="z"/>, in metres.</summary>
    public Point ToScreen(double y, double z) => new(Origin.X + y * Scale, Origin.Y - z * Scale);

    /// <summary>
    /// The sections to draw: stations 1–5 aft (left) and 5–9 forward (right). Stations 0 and 10 are left out:
    /// they are the stern and the bow, where a ship hull has no width.
    /// </summary>
    public static IReadOnlyList<BodyPlanSection> Sections(Hull hull)
    {
        const int midships = StationIntervals / 2;
        var sections = new List<BodyPlanSection>();
        for (int station = 1; station <= midships; station++)
        {
            sections.Add(Section(hull, station, isForward: false));
        }

        for (int station = midships; station < StationIntervals; station++)
        {
            sections.Add(Section(hull, station, isForward: true));
        }

        return sections;
    }

    private static BodyPlanSection Section(Hull hull, int station, bool isForward)
    {
        double x = hull.Length * station / StationIntervals;
        double side = isForward ? 1 : -1;

        // Start on the centreline at the keel. For a ship the next point is there too (zero width at the keel);
        // for a box this point draws the flat bottom out to the side.
        var points = new List<(double Y, double Z)> { (0, 0) };
        for (int i = 0; i <= PointsPerSection; i++)
        {
            double z = hull.Depth * i / PointsPerSection;  // from the index, no drift; the last is exactly D
            points.Add((side * HullGeometry.HalfBreadth(hull, x, z), z));
        }

        return new BodyPlanSection(station, x, isForward, points);
    }
}
