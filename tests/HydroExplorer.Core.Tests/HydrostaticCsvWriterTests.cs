using System.Globalization;
using HydroExplorer.Core.Export;
using HydroExplorer.Core.Hydrostatics;
using HydroExplorer.Core.Models;

namespace HydroExplorer.Core.Tests;

public class HydrostaticCsvWriterTests
{
    private static readonly Hull CoastalBarge = new(2, "Coastal Barge", Length: 80, Beam: 16, Depth: 7);

    [Fact]
    public void ToCsv_WithNoRows_WritesOnlyTheHeader()
    {
        Assert.Equal(HydrostaticCsvWriter.Header + "\r\n", HydrostaticCsvWriter.ToCsv(Array.Empty<HydrostaticRow>()));
    }

    [Fact]
    public void ToCsv_WritesOneLinePerRow_InTheHeaderOrder()
    {
        // Coastal Barge at T = 4.00 m; the values are worked out in HydrostaticsCalculatorTests.
        HydrostaticRow row = HydrostaticsCalculator.Calculate(CoastalBarge, 4.0);

        string[] lines = HydrostaticCsvWriter.ToCsv(new[] { row }).Split("\r\n");

        Assert.Equal(3, lines.Length);  // header, one row, and the empty string after the final line break
        Assert.Equal(HydrostaticCsvWriter.Header, lines[0]);
        Assert.Equal("4,5120,5248,1280,13.12,2,5.333333,7.333333,133.333333,87.466667", lines[1]);
        Assert.Equal("", lines[2]);
    }

    [Fact]
    public void ToCsv_HidesFloatingPointNoise()
    {
        // ρ · ∇ for the Large Pontoon at 11.85 m comes out as 43726.49999999999 in double arithmetic.
        var row = new HydrostaticRow(11.85, 42660, 43726.49999999999, 3600, 36.9, 5.925, 6.3, 12.2, 101.3, 369);

        string line = HydrostaticCsvWriter.ToCsv(new[] { row }).Split("\r\n")[1];

        Assert.StartsWith("11.85,42660,43726.5,", line);
    }

    [Fact]
    public void ToCsv_UsesAPointDecimalSeparator_WhateverTheUsersCulture()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("sv-SE");  // writes 3.25 as "3,25"

            string csv = HydrostaticCsvWriter.ToCsv(new[] { HydrostaticsCalculator.Calculate(CoastalBarge, 3.25) });

            Assert.StartsWith("3.25,", csv.Split("\r\n")[1]);
            Assert.Equal(10, csv.Split("\r\n")[1].Split(',').Length);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
