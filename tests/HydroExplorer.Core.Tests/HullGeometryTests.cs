using HydroExplorer.Core.Hydrostatics;
using HydroExplorer.Core.Models;

namespace HydroExplorer.Core.Tests;

public class HullGeometryTests
{
    private const double Tolerance = 1e-12;

    // L 100, B 20, D 10; fuller aft (p = 2) than fore (p = 4) would be unusual, but makes the two halves easy to tell apart.
    private static readonly Hull Ship = new(9, "Test ship", 100, 20, 10, new HullShape(BowExponent: 4, SternExponent: 2, SectionExponent: 3));
    private static readonly Hull Box = new(1, "Small Barge", 40, 10, 4);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(20, 1.5)]
    [InlineData(40, 4)]
    public void HalfBreadth_OfABox_IsHalfTheBeamEverywhere(double x, double z)
    {
        Assert.Equal(5.0, HullGeometry.HalfBreadth(Box, x, z), Tolerance);
    }

    [Fact]
    public void HalfBreadth_AmidshipsAtTheDeck_IsHalfTheBeam()
    {
        Assert.Equal(10.0, HullGeometry.HalfBreadth(Ship, x: 50, z: 10), Tolerance);
    }

    [Theory]
    [InlineData(0)]    // stern
    [InlineData(100)]  // bow
    public void HalfBreadth_AtTheEnds_IsZero(double x)
    {
        Assert.Equal(0.0, HullGeometry.HalfBreadth(Ship, x, z: 10), Tolerance);
    }

    [Fact]
    public void HalfBreadth_AtTheKeel_IsZero()
    {
        Assert.Equal(0.0, HullGeometry.HalfBreadth(Ship, x: 50, z: 0), Tolerance);
    }

    [Fact]
    public void HalfBreadth_UsesTheBowExponentForward_AndTheSternExponentAft()
    {
        // ξ = ±0.5 at x = 75 and x = 25. At the deck g = 1.
        Assert.Equal(10 * (1 - Math.Pow(0.5, 4)), HullGeometry.HalfBreadth(Ship, x: 75, z: 10), Tolerance);
        Assert.Equal(10 * (1 - Math.Pow(0.5, 2)), HullGeometry.HalfBreadth(Ship, x: 25, z: 10), Tolerance);
    }

    [Fact]
    public void HalfBreadth_FollowsTheSectionShape()
    {
        // Amidships f = 1; halfway up g = 1 − (1 − 0.5)³ = 0.875.
        Assert.Equal(10 * 0.875, HullGeometry.HalfBreadth(Ship, x: 50, z: 5), Tolerance);
    }
}
