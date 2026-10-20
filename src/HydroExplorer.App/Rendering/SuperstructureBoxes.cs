using HydroExplorer.Core.Hydrostatics;
using HydroExplorer.Core.Models;

namespace HydroExplorer.App.Rendering;

/// <summary>
/// One box of a ship's superstructure, centred on the centreline, in hull coordinates (metres):
/// from <see cref="Aft"/> to <see cref="Fore"/> along the length, ±<see cref="HalfWidth"/> across, and from
/// <see cref="Bottom"/> to <see cref="Top"/> above the keel.
/// </summary>
public readonly record struct SuperstructureBox(double Aft, double Fore, double HalfWidth, double Bottom, double Top, MeshPart Part);

/// <summary>
/// The decorative superstructure of a ship hull, as boxes. The single definition behind both drawings: the 3D
/// mesh turns each box into faces, and the side profile draws each box as a rectangle.
/// </summary>
/// <remarks>Decorative only: the hydrostatics never see it.</remarks>
public static class SuperstructureBoxes
{
    // Proportions: lengths are fractions of L from the stern, heights fractions of D above the deck.

    // Aft accommodation: a typical coaster, tanker or container feeder.
    private const double DeckhouseAft = 0.04, DeckhouseFore = 0.20, DeckhouseHeight = 0.5, DeckhouseWidth = 0.8;
    private const double BridgeAft = 0.12, BridgeHeight = 0.25;
    private const double FunnelAft = 0.06, FunnelFore = 0.10, FunnelWidth = 0.25, FunnelTop = 0.9;

    // Forward accommodation: a Service Operation Vessel. Three accommodation tiers stepping in towards the top,
    // the bridge with a helideck on its roof, and the walk-to-work gangway tower amidships with the gangway stowed
    // aft along the centreline. The aft working deck stays clear.
    private const double AccommodationAft = 0.55, AccommodationFore = 0.85, AccommodationWidth = 0.85;
    private const double TierHeight = 0.37;                         // three tiers: 1.1 D above the deck
    private const double TierSetBack = 0.05, TierNarrowing = 0.08;  // each tier starts further forward and is narrower
    private const double SovBridgeAft = 0.72, SovBridgeHeight = 0.25;
    private const double HelideckAft = 0.72, HelideckFore = 0.88, HelideckThickness = 0.05;
    private const double TowerAft = 0.44, TowerFore = 0.48, TowerWidth = 0.12, TowerHeight = 0.9;
    private const double GangwayAft = 0.22, GangwayWidth = 0.04, GangwayThickness = 0.07;

    /// <summary>
    /// The highest point of the superstructure above the deck, as a fraction of D: everything has
    /// z ≤ D · (1 + this). Zero for a box barge, which has none.
    /// </summary>
    public static double MaxHeightAboveDeck(HullShape? shape) => shape?.Superstructure switch
    {
        null => 0,
        SuperstructureLayout.ForwardAccommodation => 3 * TierHeight + SovBridgeHeight + HelideckThickness,
        _ => FunnelTop,
    };

    /// <summary>The boxes for <paramref name="hull"/>, in drawing order (lower boxes first); empty for a box barge.</summary>
    public static IReadOnlyList<SuperstructureBox> For(Hull hull) => hull.Shape?.Superstructure switch
    {
        null => Array.Empty<SuperstructureBox>(),
        SuperstructureLayout.ForwardAccommodation => ForwardAccommodation(hull),
        _ => AftAccommodation(hull),
    };

    private static List<SuperstructureBox> AftAccommodation(Hull hull)
    {
        double l = hull.Length, d = hull.Depth;

        // The deckhouse is in the aft half, where the deck narrows towards the stern, so its width is set by
        // the narrower (aft) end of its footprint.
        double deckhouseAft = DeckhouseAft * l, deckhouseFore = DeckhouseFore * l;
        double halfWidth = DeckhouseWidth * NarrowerDeckEnd(hull, deckhouseAft, deckhouseFore);
        double deckhouseTop = d + DeckhouseHeight * d;

        return new List<SuperstructureBox>
        {
            new(deckhouseAft, deckhouseFore, halfWidth, d, deckhouseTop, MeshPart.Superstructure),

            // Bridge: the forward part of the deckhouse, one level higher.
            new(BridgeAft * l, deckhouseFore, halfWidth, deckhouseTop, deckhouseTop + BridgeHeight * d, MeshPart.Superstructure),

            // Funnel: a narrow box on the deckhouse roof, aft of the bridge.
            new(FunnelAft * l, FunnelFore * l, FunnelWidth * halfWidth, deckhouseTop, d + FunnelTop * d, MeshPart.Funnel),
        };
    }

    private static List<SuperstructureBox> ForwardAccommodation(Hull hull)
    {
        double l = hull.Length, d = hull.Depth;
        var boxes = new List<SuperstructureBox>();

        // Accommodation in three tiers, in the forward half where the deck narrows towards the bow.
        double aft = AccommodationAft * l, fore = AccommodationFore * l;
        double halfWidth = AccommodationWidth * NarrowerDeckEnd(hull, aft, fore);
        double top = d;
        double tierHalfWidth = halfWidth;
        for (int tier = 0; tier < 3; tier++)
        {
            tierHalfWidth = halfWidth * (1 - TierNarrowing * tier);
            boxes.Add(new(aft + tier * TierSetBack * l, fore, tierHalfWidth, top, top + TierHeight * d, MeshPart.Superstructure));
            top += TierHeight * d;
        }

        // Bridge on the top tier, as wide as it, with the helideck on its roof overhanging the bow end.
        double bridgeTop = top + SovBridgeHeight * d;
        boxes.Add(new(SovBridgeAft * l, fore, tierHalfWidth, top, bridgeTop, MeshPart.Superstructure));
        boxes.Add(new(HelideckAft * l, HelideckFore * l, halfWidth, bridgeTop, bridgeTop + HelideckThickness * d, MeshPart.Helideck));

        // Walk-to-work gangway: a tower amidships, with the gangway boom stowed aft along the centreline on top.
        double towerTop = d + TowerHeight * d;
        boxes.Add(new(TowerAft * l, TowerFore * l, TowerWidth * hull.Beam / 2, d, towerTop, MeshPart.Gangway));
        boxes.Add(new(GangwayAft * l, TowerFore * l, GangwayWidth * hull.Beam / 2, towerTop, towerTop + GangwayThickness * d, MeshPart.Gangway));

        return boxes;
    }

    /// <summary>Half-breadth of the deck at whichever end of the footprint is narrower.</summary>
    private static double NarrowerDeckEnd(Hull hull, double aft, double fore) =>
        Math.Min(HullGeometry.HalfBreadth(hull, aft, hull.Depth), HullGeometry.HalfBreadth(hull, fore, hull.Depth));
}
