using HydroExplorer.Core.Hydrostatics;
using HydroExplorer.Core.Models;

namespace HydroExplorer.Core.Tests;

public class HydrostaticCurveSamplerTests
{
    private static readonly Hull SmallBarge = new(1, "Small Barge", Length: 40, Beam: 10, Depth: 4);

    [Fact]
    public void Sample_ReturnsRequestedNumberOfPoints_FromMinDraftToDepth()
    {
        IReadOnlyList<CurvePoint> curve = HydrostaticCurveSampler.Sample(SmallBarge, 100);

        Assert.Equal(100, curve.Count);
        Assert.Equal(0.10, curve[0].Draft);
        Assert.Equal(4.0, curve[^1].Draft);
    }

    [Fact]
    public void Sample_PointsAreEvenlySpaced()
    {
        // 0.10 to 4.00 in 4 points: step = 3.90 / 3 = 1.30 → 0.10, 1.40, 2.70, 4.00
        IReadOnlyList<CurvePoint> curve = HydrostaticCurveSampler.Sample(SmallBarge, 4);

        Assert.Equal(new[] { 0.10, 1.40, 2.70, 4.00 }, curve.Select(p => Math.Round(p.Draft, 10)));
    }

    [Fact]
    public void Sample_DisplacementMatchesCalculator()
    {
        IReadOnlyList<CurvePoint> curve = HydrostaticCurveSampler.Sample(SmallBarge, 10);

        foreach (CurvePoint point in curve)
        {
            Assert.Equal(HydrostaticsCalculator.Calculate(SmallBarge, point.Draft).Displacement, point.Displacement);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    public void Sample_FewerThanTwoPoints_Throws(int points)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HydrostaticCurveSampler.Sample(SmallBarge, points));
    }
}
