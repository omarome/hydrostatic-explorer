using HydroExplorer.Core.Models;

namespace HydroExplorer.Core.Data;

/// <summary>In-memory repository holding the sample hulls: three box barges and three ship-shaped hulls.</summary>
public sealed class SeedHullRepository : IHullRepository
{
    private static readonly IReadOnlyList<Hull> Hulls = new[]
    {
        //       Id  Name               L (m)  B (m)  D (m)
        new Hull(1, "Small Barge",       40,    10,     4),
        new Hull(2, "Coastal Barge",     80,    16,     7),
        new Hull(3, "Large Pontoon",    120,    30,    12),

        // Ship hulls. Exponents: bow p, stern p, section q (see HullShape). Cb is at full depth.
        new Hull(4, "Coastal Tanker",   100,    16,     8, new HullShape(BowExponent: 8, SternExponent: 6, SectionExponent: 10)),  // Cb ≈ 0.79
        new Hull(5, "Container Feeder", 140,    22,    11, new HullShape(BowExponent: 3, SternExponent: 3, SectionExponent: 5)),   // Cb ≈ 0.63

        // Service Operation Vessel (offshore wind). L and B follow Ulstein's SOV designs (89.6–93 m × 18–19.2 m);
        // the depth is an assumption (not published). Full stern for the large aft working deck. Cb ≈ 0.70.
        new Hull(6, "Service Operation Vessel", 90, 19,  8, new HullShape(BowExponent: 4, SternExponent: 5, SectionExponent: 6,
                                                                          Superstructure: SuperstructureLayout.ForwardAccommodation)),
    };

    public IReadOnlyList<Hull> GetAll() => Hulls;

    // FirstOrDefault returns null when nothing matches, like Array.prototype.find in JS.
    public Hull? GetById(int id) => Hulls.FirstOrDefault(hull => hull.Id == id);
}
