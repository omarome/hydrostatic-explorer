using HydroExplorer.Core.Models;

namespace HydroExplorer.Core.Hydrostatics;

/// <summary>
/// Hydrostatics for one hull at one draft. Box barges use the exact formulas below (no integration needed);
/// ship hulls are integrated numerically by <see cref="NumericalHydrostatics"/>.
/// </summary>
/// <remarks>
/// A static class has no instances and no state, like a module of pure functions in TypeScript.
/// </remarks>
public static class HydrostaticsCalculator
{
    /// <summary>Seawater density ρ (t/m³).</summary>
    public const double SeawaterDensity = 1.025;

    /// <summary>Computes all hydrostatic values for <paramref name="hull"/> at draft <paramref name="draft"/>.</summary>
    /// <exception cref="ArgumentException">The hull has a non-positive dimension.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The draft is not in (0, Depth].</exception>
    public static HydrostaticRow Calculate(Hull hull, double draft)
    {
        ValidateHull(hull);
        ValidateDraft(hull, draft);

        if (hull.Shape is not null)
        {
            return NumericalHydrostatics.Calculate(hull, draft);
        }

        // Box barge: every formula below is exact.
        double length = hull.Length;  // L
        double beam = hull.Beam;      // B
        double t = draft;             // T

        double volume = length * beam * t;                          // ∇ = L · B · T
        double displacement = SeawaterDensity * volume;             // Δ = ρ · ∇
        double waterplaneArea = length * beam;                      // Awp = L · B
        double tpc = waterplaneArea * SeawaterDensity / 100;        // TPC = Awp · ρ / 100
        double kb = t / 2;                                          // KB = T / 2
        double bm = beam * beam / (12 * t);                         // BM = B² / (12 · T)
        double km = kb + bm;                                        // KM = KB + BM
        double bml = length * length / (12 * t);                    // BML = L² / (12 · T)

        // MCT = Δ · GML / (100 · L). We approximate GML ≈ BML, which is valid for a box barge
        // because BML (tens to hundreds of metres) is much larger than KG (a few metres).
        double mct = displacement * bml / (100 * length);

        return new HydrostaticRow(
            Draft: t,
            Volume: volume,
            Displacement: displacement,
            WaterplaneArea: waterplaneArea,
            Tpc: tpc,
            Kb: kb,
            Bm: bm,
            Km: km,
            Bml: bml,
            Mct: mct);
    }

    /// <summary>Throws if any hull dimension is not a positive number.</summary>
    public static void ValidateHull(Hull hull)
    {
        // ArgumentNullException.ThrowIfNull is the standard one-line null guard.
        ArgumentNullException.ThrowIfNull(hull);
        RequirePositive(hull.Length, nameof(hull.Length));
        RequirePositive(hull.Beam, nameof(hull.Beam));
        RequirePositive(hull.Depth, nameof(hull.Depth));

        if (hull.Shape is HullShape shape)
        {
            RequirePositive(shape.BowExponent, nameof(shape.BowExponent));
            RequirePositive(shape.SternExponent, nameof(shape.SternExponent));
            RequirePositive(shape.SectionExponent, nameof(shape.SectionExponent));
        }
    }

    private static void ValidateDraft(Hull hull, double draft)
    {
        // "!(draft > 0)" rather than "draft <= 0" so that NaN is rejected too
        // (every comparison with NaN is false).
        if (!(draft > 0) || draft > hull.Depth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(draft), draft, $"Draft must be greater than 0 and at most the hull depth ({hull.Depth} m).");
        }
    }

    private static void RequirePositive(double value, string name)
    {
        // double.IsFinite rejects NaN and ±Infinity.
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentException($"Hull {name} must be a positive number, but was {value}.", name);
        }
    }
}
