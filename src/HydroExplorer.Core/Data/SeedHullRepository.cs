using HydroExplorer.Core.Models;

namespace HydroExplorer.Core.Data;

/// <summary>In-memory repository holding the three sample hulls.</summary>
public sealed class SeedHullRepository : IHullRepository
{
    private static readonly IReadOnlyList<Hull> Hulls = new[]
    {
        //       Id  Name               L (m)  B (m)  D (m)
        new Hull(1, "Small Barge",       40,    10,     4),
        new Hull(2, "Coastal Barge",     80,    16,     7),
        new Hull(3, "Large Pontoon",    120,    30,    12),
    };

    public IReadOnlyList<Hull> GetAll() => Hulls;

    // FirstOrDefault returns null when nothing matches, like Array.prototype.find in JS.
    public Hull? GetById(int id) => Hulls.FirstOrDefault(hull => hull.Id == id);
}
