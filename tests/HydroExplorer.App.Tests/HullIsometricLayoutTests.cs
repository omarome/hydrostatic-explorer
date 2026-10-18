using Avalonia;
using HydroExplorer.App.Controls;

namespace HydroExplorer.App.Tests;

public class HullIsometricLayoutTests
{
    private const double Tolerance = 1e-9;
    private static readonly Thickness Margins = HullIsometricLayout.Margins;

    // The Large Pontoon: L 120, B 30, D 12.
    private const double L = 120, B = 30, D = 12;

    private static IEnumerable<Point> Corners(HullIsometricLayout layout) =>
        from x in new[] { 0.0, L }
        from y in new[] { 0.0, B }
        from z in new[] { 0.0, D }
        select layout.Project(x, y, z);

    [Fact]
    public void Project_KeelCornerIsTheOrigin()
    {
        HullIsometricLayout layout = HullIsometricLayout.Create(new Size(800, 400), L, B, D)!;

        Assert.Equal(layout.Origin, layout.Project(0, 0, 0));
    }

    [Fact]
    public void Project_UsesTheIsometricAxes_WithOneScale()
    {
        HullIsometricLayout layout = HullIsometricLayout.Create(new Size(800, 400), L, B, D)!;
        Point origin = layout.Origin;
        double s = layout.Scale;

        // z: straight up, 1 m = Scale pixels.
        Point up = layout.Project(0, 0, 1);
        Assert.Equal(origin.X, up.X, Tolerance);
        Assert.Equal(origin.Y - s, up.Y, Tolerance);

        // x: right and up at 30°; y: left and up at 30°. Both are 1 m long on screen too.
        Point alongLength = layout.Project(1, 0, 0);
        Assert.Equal(origin.X + s * Math.Cos(Math.PI / 6), alongLength.X, Tolerance);
        Assert.Equal(origin.Y - s * 0.5, alongLength.Y, Tolerance);

        Point acrossBeam = layout.Project(0, 1, 0);
        Assert.Equal(origin.X - s * Math.Cos(Math.PI / 6), acrossBeam.X, Tolerance);
        Assert.Equal(origin.Y - s * 0.5, acrossBeam.Y, Tolerance);
    }

    [Theory]
    [InlineData(800, 400)]
    [InlineData(400, 600)]
    [InlineData(250, 160)]
    public void Create_FitsAllCornersInsideTheMargins_TouchingAtLeastOneSide(double width, double height)
    {
        HullIsometricLayout layout = HullIsometricLayout.Create(new Size(width, height), L, B, D)!;
        Point[] corners = Corners(layout).ToArray();

        double left = corners.Min(p => p.X), right = corners.Max(p => p.X);
        double top = corners.Min(p => p.Y), bottom = corners.Max(p => p.Y);
        Assert.True(left >= Margins.Left - 1e-6);
        Assert.True(right <= width - Margins.Right + 1e-6);
        Assert.True(top >= Margins.Top - 1e-6);
        Assert.True(bottom <= height - Margins.Bottom + 1e-6);

        double freeWidth = width - Margins.Left - Margins.Right;
        double freeHeight = height - Margins.Top - Margins.Bottom;
        Assert.True(Math.Abs(right - left - freeWidth) < 1e-6 || Math.Abs(bottom - top - freeHeight) < 1e-6);
    }

    [Fact]
    public void Project_WaterplaneSitsBetweenKeelAndDeck()
    {
        HullIsometricLayout layout = HullIsometricLayout.Create(new Size(800, 400), L, B, D)!;

        double keelY = layout.Project(0, 0, 0).Y;
        double waterY = layout.Project(0, 0, 4).Y;
        double deckY = layout.Project(0, 0, D).Y;

        Assert.True(deckY < waterY && waterY < keelY);  // screen y grows downwards
        Assert.Equal(4.0 / D, (keelY - waterY) / (keelY - deckY), Tolerance);
    }

    [Theory]
    [InlineData(0, 30, 12)]
    [InlineData(120, 0, 12)]
    [InlineData(120, 30, 0)]
    public void Create_WithNoHull_ReturnsNull(double length, double beam, double depth)
    {
        Assert.Null(HullIsometricLayout.Create(new Size(800, 400), length, beam, depth));
    }

    [Fact]
    public void Create_WithNoRoomInsideTheMargins_ReturnsNull()
    {
        Assert.Null(HullIsometricLayout.Create(new Size(90, 60), L, B, D));
    }
}
