using HydroExplorer.Core.Data;
using HydroExplorer.Core.Hydrostatics;
using HydroExplorer.Core.Models;

namespace HydroExplorer.Core.Tests;

/// <summary>
/// The integrator checked three ways: against the exact box formulas, against the analytic Wigley hull,
/// and by convergence (more stations or waterlines barely change the answer).
/// </summary>
public class NumericalHydrostaticsTests
{
    private static readonly SeedHullRepository Seeds = new();

    // ---- 1. A box: Simpson's rule is exact, so it must reproduce the box formulas ----

    [Theory]
    [InlineData(1, 0.10)]
    [InlineData(1, 2.37)]
    [InlineData(2, 7.00)]
    [InlineData(3, 5.55)]
    public void Box_MatchesTheExactBoxFormulas_ForEveryField(int hullId, double draft)
    {
        Hull box = Seeds.GetById(hullId)!;  // ids 1–3 are box barges (asserted in SeedHullRepositoryTests)

        HydrostaticRow exact = HydrostaticsCalculator.Calculate(box, draft);
        HydrostaticRow integrated = NumericalHydrostatics.Calculate(box, draft);

        AssertRelative(exact.Draft, integrated.Draft, 1e-9);
        AssertRelative(exact.Volume, integrated.Volume, 1e-9);
        AssertRelative(exact.Displacement, integrated.Displacement, 1e-9);
        AssertRelative(exact.WaterplaneArea, integrated.WaterplaneArea, 1e-9);
        AssertRelative(exact.Tpc, integrated.Tpc, 1e-9);
        AssertRelative(exact.Kb, integrated.Kb, 1e-9);
        AssertRelative(exact.Bm, integrated.Bm, 1e-9);
        AssertRelative(exact.Km, integrated.Km, 1e-9);
        AssertRelative(exact.Bml, integrated.Bml, 1e-9);
        AssertRelative(exact.Mct, integrated.Mct, 1e-9);
    }

    // ---- 2. The Wigley hull (p = q = 2), which has closed-form answers ----

    private const double L = 100, B = 10, D = 8;
    private static readonly Hull Wigley = new(9, "Wigley", L, B, D, new HullShape(2, 2, 2));

    /// <summary>Section shape g(z) = 1 − (1 − z/D)² for q = 2.</summary>
    private static double G(double z) => 1 - Math.Pow(1 - z / D, 2);

    [Fact]
    public void Wigley_FullDepthVolume_Is4NinthsOfTheBox()
    {
        HydrostaticRow row = NumericalHydrostatics.Calculate(Wigley, D);

        AssertRelative(4.0 / 9.0 * L * B * D, row.Volume, 1e-4);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(4.0)]
    [InlineData(8.0)]
    public void Wigley_WaterplaneArea_IsTwoThirdsOfLTimesBTimesG(double draft)
    {
        HydrostaticRow row = NumericalHydrostatics.Calculate(Wigley, draft);

        AssertRelative(2.0 / 3.0 * L * B * G(draft), row.WaterplaneArea, 1e-4);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(4.0)]
    [InlineData(8.0)]
    public void Wigley_Kb_MatchesTheAnalyticCentroid(double draft)
    {
        // Awp(z) ∝ g(z) = 2s − s² with s = z/D, so with τ = T/D:
        //   KB = ∫ z·g dz / ∫ g dz = D · (2τ³/3 − τ⁴/4) / (τ² − τ³/3)
        double tau = draft / D;
        double expected = D * (2 * Math.Pow(tau, 3) / 3 - Math.Pow(tau, 4) / 4) / (tau * tau - Math.Pow(tau, 3) / 3);

        AssertRelative(expected, NumericalHydrostatics.Calculate(Wigley, draft).Kb, 1e-4);
    }

    [Theory]
    [InlineData(2.0)]
    [InlineData(6.0)]
    public void Wigley_Bm_UsesTheAnalyticTransverseMoment(double draft)
    {
        // IT = (2/3) ∫ y³ dx with y = (B/2)·(1 − ξ²)·g and dx = (L/2) dξ; ∫₋₁¹ (1 − ξ²)³ dξ = 32/35.
        double it = 2.0 / 3.0 * Math.Pow(B / 2 * G(draft), 3) * (L / 2) * (32.0 / 35.0);
        HydrostaticRow row = NumericalHydrostatics.Calculate(Wigley, draft);

        AssertRelative(it / row.Volume, row.Bm, 1e-4);
    }

    [Theory]
    [InlineData(2.0)]
    [InlineData(6.0)]
    public void Wigley_Bml_UsesTheAnalyticLongitudinalMoment(double draft)
    {
        // Symmetric fore and aft, so LCF = L/2. IL = 2 ∫ (x − L/2)²·y dx = B·g·(L/2)³ · ∫₋₁¹ ξ²(1 − ξ²) dξ,
        // and that last integral is 2/3 − 2/5 = 4/15.
        double il = B * G(draft) * Math.Pow(L / 2, 3) * (4.0 / 15.0);
        HydrostaticRow row = NumericalHydrostatics.Calculate(Wigley, draft);

        AssertRelative(il / row.Volume, row.Bml, 1e-4);
    }

    // ---- 3. The seed ship hulls ----

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void SeedShip_BlockCoefficientAtFullDepth_MatchesTheFormula(int hullId)
    {
        Hull ship = Seeds.GetById(hullId)!;  // ids 4–5 are ship hulls (asserted in SeedHullRepositoryTests)
        HydrostaticRow row = NumericalHydrostatics.Calculate(ship, ship.Depth);

        double cb = row.Volume / (ship.Length * ship.Beam * ship.Depth);

        Assert.Equal(ship.Shape!.FullDepthBlockCoefficient, cb, 3);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void SeedShip_VolumeIncreases_AndWaterplaneAreaNeverDecreases_WithDraft(int hullId)
    {
        IReadOnlyList<HydrostaticRow> rows = HydrostaticTableGenerator.Generate(Seeds.GetById(hullId)!);

        for (int i = 1; i < rows.Count; i++)
        {
            Assert.True(rows[i].Volume > rows[i - 1].Volume, $"Volume did not increase at T = {rows[i].Draft}");
            // Only "never decreases" for Awp: near the deck the tanker's sides are so vertical (q = 10) that
            // (1 − T/D)^10 drops below 1e-15 and consecutive rows have the same Awp to double precision.
            Assert.True(rows[i].WaterplaneArea >= rows[i - 1].WaterplaneArea, $"Awp decreased at T = {rows[i].Draft}");
        }
    }

    [Theory]
    [InlineData(4, 0.5)]
    [InlineData(4, 8.0)]
    [InlineData(5, 0.5)]
    [InlineData(5, 11.0)]
    public void SeedShip_Converges_WhenStationsOrWaterlinesAreDoubled(int hullId, double draft)
    {
        // Tolerance 1e-4, not the plan's 1e-6: the tanker's high exponents leave ~1e-5 error at the default
        // grid (see DECISIONS.md). That's about 0.1 t on 10,000 t, far below what the table shows.
        Hull ship = Seeds.GetById(hullId)!;
        double baseline = NumericalHydrostatics.Calculate(ship, draft).Volume;

        double moreStations = NumericalHydrostatics.Calculate(ship, draft, stations: 81).Volume;
        double moreWaterlines = NumericalHydrostatics.Calculate(ship, draft, waterlines: 41).Volume;

        AssertRelative(moreStations, baseline, 1e-4);
        AssertRelative(moreWaterlines, baseline, 1e-4);
    }

    [Theory]
    [InlineData(40)]  // stations: (40 − 1) is odd, so the halves can't be integrated separately
    [InlineData(43)]  // odd, but each half would have an odd number of intervals
    public void Calculate_RejectsStationCountsThatDontSplitAtMidships(int stations)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NumericalHydrostatics.Calculate(Wigley, 4, stations: stations));
    }

    private static void AssertRelative(double expected, double actual, double tolerance)
    {
        double error = Math.Abs(actual - expected) / Math.Abs(expected);
        Assert.True(error <= tolerance, $"Expected {expected}, got {actual} (relative error {error:E2} > {tolerance:E0}).");
    }
}
