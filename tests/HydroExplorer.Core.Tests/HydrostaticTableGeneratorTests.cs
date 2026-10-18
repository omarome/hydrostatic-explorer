using HydroExplorer.Core.Data;
using HydroExplorer.Core.Hydrostatics;
using HydroExplorer.Core.Models;

namespace HydroExplorer.Core.Tests;

public class HydrostaticTableGeneratorTests
{
    private readonly SeedHullRepository _repository = new();

    // Rows run from 0.10 m to D in 1 cm steps: (D·100 − 10) + 1 rows.
    [Theory]
    [InlineData(1, 391)]   // Small Barge,   D = 4 m:  400 − 10 + 1
    [InlineData(2, 691)]   // Coastal Barge, D = 7 m:  700 − 10 + 1
    [InlineData(3, 1191)]  // Large Pontoon, D = 12 m: 1200 − 10 + 1
    public void Generate_ProducesOneRowPerCentimetre(int hullId, int expectedRows)
    {
        Hull hull = _repository.GetById(hullId)!;

        IReadOnlyList<HydrostaticRow> rows = HydrostaticTableGenerator.Generate(hull);

        Assert.Equal(expectedRows, rows.Count);
    }

    [Fact]
    public void Generate_FirstDraftIs10cm_AndLastDraftIsDepth()
    {
        Hull hull = _repository.GetById(3)!;

        IReadOnlyList<HydrostaticRow> rows = HydrostaticTableGenerator.Generate(hull);

        Assert.Equal(0.10, rows[0].Draft);
        Assert.Equal(12.0, rows[^1].Draft);  // [^1] means "last element", like rows.at(-1) in JS
    }

    [Fact]
    public void Generate_DraftsAreExactTwoDecimalValues_WithNoDrift()
    {
        Hull hull = _repository.GetById(3)!;

        IReadOnlyList<HydrostaticRow> rows = HydrostaticTableGenerator.Generate(hull);

        // Exact equality (no tolerance) on purpose: row i must hold exactly the double for (10 + i) / 100.
        // Repeatedly adding 0.01 would fail this (e.g. row 315 would be 3.2499999999999747).
        Assert.Equal(3.25, rows[315].Draft);
        for (int i = 0; i < rows.Count; i++)
        {
            Assert.Equal(Math.Round((10 + i) / 100.0, 2), rows[i].Draft);
        }
    }

    [Fact]
    public void Generate_RowsMatchCalculatorAtSameDraft()
    {
        Hull hull = _repository.GetById(2)!;

        IReadOnlyList<HydrostaticRow> rows = HydrostaticTableGenerator.Generate(hull);

        // Record equality compares every property value, so this checks the whole row.
        Assert.Equal(HydrostaticsCalculator.Calculate(hull, 4.0), rows.Single(r => r.Draft == 4.0));
    }

    [Fact]
    public void Generate_InvalidHull_Throws()
    {
        var hull = new Hull(99, "Bad", Length: 0, Beam: 10, Depth: 4);

        Assert.Throws<ArgumentException>(() => HydrostaticTableGenerator.Generate(hull));
    }
}
