using HydroExplorer.Core.Models;

namespace HydroExplorer.Core.Hydrostatics;

/// <summary>
/// Hydrostatics of any hull by numerical integration of its half-breadths (Simpson's rule), the way they are
/// computed from a ship's offsets table. Used for ship hulls; for a box it reproduces the exact formulas.
/// </summary>
/// <remarks>
/// <para>Along the length the hull is sampled at <c>stations</c> points with midships as a station, and each half
/// (aft and fore) is integrated separately, because the hull form uses a different exponent in each half and
/// Simpson's rule needs a smooth function over each piece.</para>
/// <para>Formulas (y = half-breadth, all integrals over the length unless marked):</para>
/// <code>
///   Awp(T) = 2 ∫ y(x,T) dx                    waterplane area
///   LCF    = 2 ∫ x·y(x,T) dx / Awp            longitudinal centre of flotation (from the stern)
///   ∇(T)   = ∫₀ᵀ Awp(z) dz                    volume
///   KB     = ∫₀ᵀ z·Awp(z) dz / ∇
///   IT     = (2/3) ∫ y(x,T)³ dx               transverse 2nd moment of the waterplane
///   IL     = 2 ∫ (x − LCF)²·y(x,T) dx         longitudinal 2nd moment about LCF
///   BM = IT/∇,  KM = KB + BM,  BML = IL/∇,  Δ = ρ∇,  TPC = Awp·ρ/100,  MCT = Δ·BML/(100·L)
/// </code>
/// </remarks>
public static class NumericalHydrostatics
{
    /// <summary>Default number of stations along the length: 20 intervals in each half.</summary>
    public const int DefaultStations = 41;

    /// <summary>Default number of waterlines from the keel up to the draft: 20 intervals.</summary>
    public const int DefaultWaterlines = 21;

    /// <summary>Computes all hydrostatic values for <paramref name="hull"/> at <paramref name="draft"/>.</summary>
    /// <param name="stations">Odd, with (stations − 1) divisible by 4 so each half has an even number of intervals.</param>
    /// <param name="waterlines">Odd, at least 3.</param>
    /// <exception cref="ArgumentOutOfRangeException">A grid size is invalid.</exception>
    /// <remarks>The caller (<see cref="HydrostaticsCalculator"/>) validates the hull and the draft.</remarks>
    public static HydrostaticRow Calculate(Hull hull, double draft, int stations = DefaultStations, int waterlines = DefaultWaterlines)
    {
        if (stations < 5 || (stations - 1) % 4 != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stations), stations, "Stations must be 5, 9, 13, …, so that midships is a station and each half has an even number of intervals.");
        }

        if (waterlines < 3 || waterlines % 2 == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(waterlines), waterlines, "Waterlines must be an odd number of at least 3.");
        }

        double rho = HydrostaticsCalculator.SeawaterDensity;
        double length = hull.Length;
        double t = draft;
        int pointsPerHalf = (stations - 1) / 2 + 1;

        // ---- The waterplane at the draft ----
        double y(double x) => HullGeometry.HalfBreadth(hull, x, t);  // local function: half-breadth at this waterline

        double waterplaneArea = 2 * AlongLength(y, length, pointsPerHalf);                     // Awp
        double lcf = 2 * AlongLength(x => x * y(x), length, pointsPerHalf) / waterplaneArea;  // LCF
        double it = 2.0 / 3.0 * AlongLength(x => Math.Pow(y(x), 3), length, pointsPerHalf);   // IT
        double il = 2 * AlongLength(x => (x - lcf) * (x - lcf) * y(x), length, pointsPerHalf); // IL about LCF

        // ---- Volume and KB: integrate the waterplane areas from the keel up to the draft ----
        // Sample Awp(z) once per waterline, then integrate both Awp and z·Awp from the same samples.
        double spacing = t / (waterlines - 1);
        var areas = new double[waterlines];
        var moments = new double[waterlines];
        for (int i = 0; i < waterlines; i++)
        {
            double z = i * spacing;
            areas[i] = 2 * AlongLength(x => HullGeometry.HalfBreadth(hull, x, z), length, pointsPerHalf);
            moments[i] = z * areas[i];
        }

        double volume = Simpson.Integrate(areas, spacing);       // ∇
        double kb = Simpson.Integrate(moments, spacing) / volume; // KB

        // ---- Derived values: the same definitions as the box formulas ----
        double displacement = rho * volume;            // Δ = ρ · ∇
        double tpc = waterplaneArea * rho / 100;       // TPC = Awp · ρ / 100
        double bm = it / volume;                       // BM = IT / ∇
        double km = kb + bm;                           // KM = KB + BM
        double bml = il / volume;                      // BML = IL / ∇
        double mct = displacement * bml / (100 * length);  // MCT with GML ≈ BML, as for the box

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

    /// <summary>∫₀ᴸ <paramref name="f"/>(x) dx, as the aft half plus the fore half (split at midships).</summary>
    private static double AlongLength(Func<double, double> f, double length, int pointsPerHalf)
    {
        double midships = length / 2;
        return Simpson.Integrate(f, 0, midships, pointsPerHalf) + Simpson.Integrate(f, midships, length, pointsPerHalf);
    }
}
