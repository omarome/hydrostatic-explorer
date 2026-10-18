using HydroExplorer.Core.Hydrostatics;
using HydroExplorer.Core.Models;

namespace HydroExplorer.Core.Tests;

/// <summary>
/// Each formula checked against values worked out by hand.
/// Doubles are compared with a tolerance because floating-point results are rarely bit-exact.
/// </summary>
public class HydrostaticsCalculatorTests
{
    private const double Tolerance = 1e-9;

    private static readonly Hull CoastalBarge = new(2, "Coastal Barge", Length: 80, Beam: 16, Depth: 7);
    private static readonly Hull SmallBarge = new(1, "Small Barge", Length: 40, Beam: 10, Depth: 4);

    // Hand calculation, Coastal Barge (L = 80, B = 16) at T = 4.00 m, ρ = 1.025:
    //   ∇   = 80 · 16 · 4               = 5120 m³
    //   Δ   = 1.025 · 5120              = 5248 t
    //   Awp = 80 · 16                   = 1280 m²
    //   TPC = 1280 · 1.025 / 100        = 13.12 t/cm
    //   KB  = 4 / 2                     = 2 m
    //   BM  = 16² / (12 · 4) = 256 / 48 = 16/3   ≈ 5.3333 m
    //   KM  = 2 + 16/3                  = 22/3   ≈ 7.3333 m
    //   BML = 80² / (12 · 4) = 6400/48  = 400/3  ≈ 133.333 m
    //   MCT = 5248 · (400/3) / (100·80) = 1312/15 ≈ 87.4667 t·m/cm

    [Fact]
    public void Calculate_CoastalBargeAt4m_MatchesHandCalculation()
    {
        HydrostaticRow row = HydrostaticsCalculator.Calculate(CoastalBarge, 4.0);

        Assert.Equal(4.0, row.Draft, Tolerance);
        Assert.Equal(5120.0, row.Volume, Tolerance);
        Assert.Equal(5248.0, row.Displacement, Tolerance);
        Assert.Equal(1280.0, row.WaterplaneArea, Tolerance);
        Assert.Equal(13.12, row.Tpc, Tolerance);
        Assert.Equal(2.0, row.Kb, Tolerance);
        Assert.Equal(16.0 / 3, row.Bm, Tolerance);
        Assert.Equal(22.0 / 3, row.Km, Tolerance);
        Assert.Equal(400.0 / 3, row.Bml, Tolerance);
        Assert.Equal(1312.0 / 15, row.Mct, Tolerance);
    }

    // Hand calculation, Small Barge (L = 40, B = 10) at T = 2.00 m:
    //   ∇ = 800 m³, Δ = 820 t, Awp = 400 m², TPC = 4.1 t/cm, KB = 1 m,
    //   BM = 100/24 = 25/6 m, KM = 1 + 25/6 = 31/6 m, BML = 1600/24 = 200/3 m,
    //   MCT = 820 · (200/3) / 4000 = 41/3 t·m/cm

    [Fact]
    public void Calculate_SmallBargeAt2m_MatchesHandCalculation()
    {
        HydrostaticRow row = HydrostaticsCalculator.Calculate(SmallBarge, 2.0);

        Assert.Equal(800.0, row.Volume, Tolerance);
        Assert.Equal(820.0, row.Displacement, Tolerance);
        Assert.Equal(400.0, row.WaterplaneArea, Tolerance);
        Assert.Equal(4.1, row.Tpc, Tolerance);
        Assert.Equal(1.0, row.Kb, Tolerance);
        Assert.Equal(25.0 / 6, row.Bm, Tolerance);
        Assert.Equal(31.0 / 6, row.Km, Tolerance);
        Assert.Equal(200.0 / 3, row.Bml, Tolerance);
        Assert.Equal(41.0 / 3, row.Mct, Tolerance);
    }

    [Fact]
    public void Calculate_AtFullDepth_IsAllowed()
    {
        HydrostaticRow row = HydrostaticsCalculator.Calculate(SmallBarge, 4.0);

        Assert.Equal(4.0, row.Draft);
    }

    // [Theory] + [InlineData] runs the same test once per data row (like test.each in Jest).
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(4.01)]
    [InlineData(double.NaN)]
    public void Calculate_DraftOutsideRange_Throws(double draft)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HydrostaticsCalculator.Calculate(SmallBarge, draft));
    }

    [Theory]
    [InlineData(0, 10, 4)]
    [InlineData(40, -10, 4)]
    [InlineData(40, 10, 0)]
    [InlineData(double.PositiveInfinity, 10, 4)]
    public void Calculate_HullWithNonPositiveDimension_Throws(double length, double beam, double depth)
    {
        var hull = new Hull(99, "Bad", length, beam, depth);

        Assert.Throws<ArgumentException>(() => HydrostaticsCalculator.Calculate(hull, 1.0));
    }
}
