using Avalonia;
using HydroExplorer.App.Controls;

namespace HydroExplorer.App.Tests;

public class HullProfileLayoutTests
{
    private const double Tolerance = 1e-9;
    private static readonly Thickness Margins = HullProfileLayout.Margins;

    // Several control sizes: wide, tall, small. These are the sizes a window resize produces.
    [Theory]
    [InlineData(800, 300)]
    [InlineData(400, 600)]
    [InlineData(300, 150)]
    public void Create_KeepsTheHullsAspectRatio(double width, double height)
    {
        HullProfileLayout layout = HullProfileLayout.Create(new Size(width, height), length: 120, depth: 12)!;

        Assert.Equal(120.0 / 12.0, layout.Hull.Width / layout.Hull.Height, Tolerance);
    }

    [Theory]
    [InlineData(800, 300)]
    [InlineData(400, 600)]
    [InlineData(300, 150)]
    public void Create_FitsInsideTheMargins_TouchingAtLeastOneSide(double width, double height)
    {
        HullProfileLayout layout = HullProfileLayout.Create(new Size(width, height), length: 40, depth: 4)!;
        Rect hull = layout.Hull;

        Assert.True(hull.Left >= Margins.Left - Tolerance);
        Assert.True(hull.Top >= Margins.Top - Tolerance);
        Assert.True(hull.Right <= width - Margins.Right + Tolerance);
        Assert.True(hull.Bottom <= height - Margins.Bottom + Tolerance);

        // "Scaled to fit": the limiting direction fills all the space it has.
        double freeWidth = width - Margins.Left - Margins.Right;
        double freeHeight = height - Margins.Top - Margins.Bottom;
        Assert.True(Math.Abs(hull.Width - freeWidth) < 1e-6 || Math.Abs(hull.Height - freeHeight) < 1e-6);
    }

    [Fact]
    public void Create_CentresTheHullInTheFreeSpace()
    {
        // Wide control: height limits the scale, so there is spare width split equally left and right.
        HullProfileLayout layout = HullProfileLayout.Create(new Size(1000, 200), length: 40, depth: 4)!;

        double spaceLeft = layout.Hull.Left - Margins.Left;
        double spaceRight = 1000 - Margins.Right - layout.Hull.Right;
        Assert.Equal(spaceLeft, spaceRight, Tolerance);
    }

    [Fact]
    public void WaterlineY_IsDraftTimesScaleAboveTheKeel_ClampedToTheHull()
    {
        HullProfileLayout layout = HullProfileLayout.Create(new Size(800, 300), length: 40, depth: 4)!;

        Assert.Equal(layout.KeelY, layout.WaterlineY(0), Tolerance);
        Assert.Equal(layout.KeelY - 2 * layout.Scale, layout.WaterlineY(2), Tolerance);
        Assert.Equal(layout.Hull.Top, layout.WaterlineY(4), Tolerance);  // T = D: waterline at deck
        Assert.Equal(layout.Hull.Top, layout.WaterlineY(9), Tolerance);  // above the deck: clamped
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(40, 0)]
    public void Create_WithNoHull_ReturnsNull(double length, double depth)
    {
        Assert.Null(HullProfileLayout.Create(new Size(800, 300), length, depth));
    }

    [Fact]
    public void Create_WithNoRoomInsideTheMargins_ReturnsNull()
    {
        Assert.Null(HullProfileLayout.Create(new Size(100, 60), length: 40, depth: 4));
    }

    // ---- Room for a superstructure above the deck ----

    [Theory]
    [InlineData(800, 300)]
    [InlineData(400, 600)]
    public void Create_WithASuperstructure_FitsItInsideTheMargins_AndKeepsTheAspectRatio(double width, double height)
    {
        // The SOV: 90 × 8 m hull, superstructure up to 1.41 D above the deck.
        HullProfileLayout layout = HullProfileLayout.Create(new Size(width, height), length: 90, depth: 8, heightAboveDeck: 1.41 * 8)!;

        Assert.Equal(90.0 / 8.0, layout.Hull.Width / layout.Hull.Height, Tolerance);
        Assert.True(layout.ToScreen(0, 8 + 1.41 * 8).Y >= Margins.Top - Tolerance, "superstructure above the top margin");
        Assert.True(layout.Hull.Bottom <= height - Margins.Bottom + Tolerance);
    }

    [Fact]
    public void Create_WithASuperstructure_DrawsTheHullSmaller_ToMakeRoom()
    {
        // A wide, short control, so height is what limits the scale.
        var size = new Size(1000, 200);

        HullProfileLayout plain = HullProfileLayout.Create(size, length: 40, depth: 10)!;
        HullProfileLayout withSuperstructure = HullProfileLayout.Create(size, length: 40, depth: 10, heightAboveDeck: 10)!;

        Assert.True(withSuperstructure.Scale < plain.Scale);
    }

    [Fact]
    public void ToScreen_MapsHullMetresToPixels()
    {
        HullProfileLayout layout = HullProfileLayout.Create(new Size(800, 300), length: 40, depth: 4)!;

        Assert.Equal(new Point(layout.Hull.Left, layout.KeelY), layout.ToScreen(0, 0));
        Assert.Equal(layout.Hull.Right, layout.ToScreen(40, 0).X, Tolerance);
        Assert.Equal(layout.Hull.Top, layout.ToScreen(0, 4).Y, Tolerance);
        Assert.Equal(layout.Hull.Top - 2 * layout.Scale, layout.ToScreen(0, 6).Y, Tolerance);  // not clamped, unlike WaterlineY
    }
}
