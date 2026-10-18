using HydroExplorer.Core.Hydrostatics;
using HydroExplorer.Core.Models;

namespace HydroExplorer.Core.Tests;

/// <summary>Snapping arbitrary drafts to table rows, and reading displacement off the sampled curve.</summary>
public class DraftSelectionMathTests
{
    private static readonly Hull SmallBarge = new(1, "Small Barge", Length: 40, Beam: 10, Depth: 4);

    [Theory]
    [InlineData(3.2547, 3.25)]
    [InlineData(3.2561, 3.26)]
    [InlineData(0.0, 0.10)]    // below the table: first row
    [InlineData(-5.0, 0.10)]
    [InlineData(4.3, 4.00)]    // above the depth: last row
    [InlineData(1e12, 4.00)]
    public void SnapToTableDraft_ReturnsNearestTableDraft(double draft, double expected)
    {
        Assert.Equal(expected, HydrostaticTableGenerator.SnapToTableDraft(SmallBarge, draft));
    }

    [Fact]
    public void SnapToTableDraft_ResultEqualsARowDraftExactly()
    {
        IReadOnlyList<HydrostaticRow> rows = HydrostaticTableGenerator.Generate(SmallBarge);

        double snapped = HydrostaticTableGenerator.SnapToTableDraft(SmallBarge, 2.17349);

        // Exact equality: the table can find the row with ==, no tolerance needed.
        Assert.Contains(rows, row => row.Draft == snapped);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void SnapToTableDraft_NonFiniteDraft_Throws(double draft)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HydrostaticTableGenerator.SnapToTableDraft(SmallBarge, draft));
    }

    [Fact]
    public void InterpolateDisplacement_MatchesCalculator_BetweenSamplePoints()
    {
        IReadOnlyList<CurvePoint> curve = HydrostaticCurveSampler.Sample(SmallBarge, 10);

        // 2.17 m is between sample points; a box hull's curve is a straight line, so interpolation is exact.
        double expected = HydrostaticsCalculator.Calculate(SmallBarge, 2.17).Displacement;
        Assert.Equal(expected, CurveInterpolator.InterpolateDisplacement(curve, 2.17)!.Value, 1e-9);
    }

    [Fact]
    public void InterpolateDisplacement_AtCurveEnds_ReturnsEndValues()
    {
        IReadOnlyList<CurvePoint> curve = HydrostaticCurveSampler.Sample(SmallBarge, 10);

        Assert.Equal(curve[0].Displacement, CurveInterpolator.InterpolateDisplacement(curve, curve[0].Draft));
        Assert.Equal(curve[^1].Displacement, CurveInterpolator.InterpolateDisplacement(curve, curve[^1].Draft));
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(4.5)]
    public void InterpolateDisplacement_OutsideCurve_ReturnsNull(double draft)
    {
        IReadOnlyList<CurvePoint> curve = HydrostaticCurveSampler.Sample(SmallBarge, 10);

        Assert.Null(CurveInterpolator.InterpolateDisplacement(curve, draft));
    }

    [Fact]
    public void InterpolateDisplacement_EmptyCurve_ReturnsNull()
    {
        Assert.Null(CurveInterpolator.InterpolateDisplacement(Array.Empty<CurvePoint>(), 1.0));
    }
}
