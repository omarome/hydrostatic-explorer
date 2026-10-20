using Avalonia;
using HydroExplorer.App.Controls;
using HydroExplorer.Core.Data;
using HydroExplorer.Core.Hydrostatics;
using HydroExplorer.Core.Models;

namespace HydroExplorer.App.Tests;

public class BodyPlanLayoutTests
{
    private const double Tolerance = 1e-9;
    private static readonly SeedHullRepository Seeds = new();
    private static readonly Hull Feeder = Seeds.GetById(5)!;  // 140 × 22 × 11, a ship
    private static readonly Hull Barge = Seeds.GetById(2)!;   // 80 × 16 × 7, a box

    // ---- Sections ----

    [Fact]
    public void Sections_AftHalfOnTheLeft_ForeHalfOnTheRight_MidshipsOnBoth()
    {
        IReadOnlyList<BodyPlanSection> sections = BodyPlanLayout.Sections(Feeder);

        // Stations 1–9 of 10 (0 and 10 are the stern and bow, where a ship has no width).
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, sections.Where(s => !s.IsForward).Select(s => s.Station));
        Assert.Equal(new[] { 5, 6, 7, 8, 9 }, sections.Where(s => s.IsForward).Select(s => s.Station));
        Assert.All(sections.Where(s => !s.IsForward).SelectMany(s => s.Points), p => Assert.True(p.Y <= 0));
        Assert.All(sections.Where(s => s.IsForward).SelectMany(s => s.Points), p => Assert.True(p.Y >= 0));
    }

    [Fact]
    public void Sections_FollowTheHullsHalfBreadth_FromKeelToDeck()
    {
        foreach (BodyPlanSection section in BodyPlanLayout.Sections(Feeder))
        {
            Assert.Equal((0, 0), (section.Points[0].Y, section.Points[0].Z));  // starts on the centreline at the keel
            Assert.Equal(Feeder.Depth, section.Points[^1].Z, Tolerance);       // ends at the deck
            foreach ((double y, double z) in section.Points.Skip(1))
            {
                Assert.Equal(HullGeometry.HalfBreadth(Feeder, section.X, z), Math.Abs(y), Tolerance);
            }
        }
    }

    [Fact]
    public void TheMidshipSection_IsTheWidest()
    {
        IReadOnlyList<BodyPlanSection> sections = BodyPlanLayout.Sections(Feeder);
        double Width(BodyPlanSection s) => s.Points.Max(p => Math.Abs(p.Y));

        Assert.Equal(sections.Max(Width), Width(sections.First(s => s.Station == 5)), Tolerance);
        Assert.Equal(Feeder.Beam / 2, Width(sections.First(s => s.Station == 5)), Tolerance);
    }

    [Fact]
    public void ABox_HasRectangularSections_AllTheSame()
    {
        IReadOnlyList<BodyPlanSection> sections = BodyPlanLayout.Sections(Barge);

        Assert.All(sections, s => Assert.All(s.Points.Skip(1), p => Assert.Equal(Barge.Beam / 2, Math.Abs(p.Y), Tolerance)));
    }

    // ---- Layout ----

    [Theory]
    [InlineData(600, 300)]
    [InlineData(300, 500)]
    public void Create_KeepsTheTrueBeamToDepthRatio_InsideTheMargins(double width, double height)
    {
        BodyPlanLayout layout = BodyPlanLayout.Create(new Size(width, height), beam: 22, depth: 11)!;
        Point bottomLeft = layout.ToScreen(-11, 0), topRight = layout.ToScreen(11, 11);

        Assert.Equal(22.0 / 11.0, (topRight.X - bottomLeft.X) / (bottomLeft.Y - topRight.Y), Tolerance);
        Assert.True(bottomLeft.X >= BodyPlanLayout.Margins.Left - Tolerance);
        Assert.True(topRight.X <= width - BodyPlanLayout.Margins.Right + Tolerance);
        Assert.True(topRight.Y >= BodyPlanLayout.Margins.Top - Tolerance);
        Assert.True(bottomLeft.Y <= height - BodyPlanLayout.Margins.Bottom + Tolerance);
    }

    [Fact]
    public void ToScreen_PutsTheCentrelineInTheMiddle()
    {
        BodyPlanLayout layout = BodyPlanLayout.Create(new Size(600, 300), beam: 22, depth: 11)!;

        double free = 600 - BodyPlanLayout.Margins.Left - BodyPlanLayout.Margins.Right;
        Assert.Equal(BodyPlanLayout.Margins.Left + free / 2, layout.ToScreen(0, 0).X, Tolerance);
    }

    [Theory]
    [InlineData(0, 11)]
    [InlineData(22, 0)]
    public void Create_WithNoHull_ReturnsNull(double beam, double depth)
    {
        Assert.Null(BodyPlanLayout.Create(new Size(600, 300), beam, depth));
    }
}
