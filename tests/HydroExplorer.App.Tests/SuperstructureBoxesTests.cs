using System.Numerics;
using HydroExplorer.App.Rendering;
using HydroExplorer.Core.Data;
using HydroExplorer.Core.Models;

namespace HydroExplorer.App.Tests;

public class SuperstructureBoxesTests
{
    private const double Tolerance = 1e-4;
    private static readonly SeedHullRepository Seeds = new();

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void ABoxBarge_HasNoSuperstructure(int hullId)
    {
        Assert.Empty(SuperstructureBoxes.For(Seeds.GetById(hullId)!));
        Assert.Equal(0, SuperstructureBoxes.MaxHeightAboveDeck(null));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Boxes_StandOnTheDeck_WithinTheHull(int hullId)
    {
        Hull hull = Seeds.GetById(hullId)!;

        Assert.All(SuperstructureBoxes.For(hull), box =>
        {
            Assert.True(box.Bottom >= hull.Depth - Tolerance, "below the deck");
            Assert.True(box.Top > box.Bottom && box.Fore > box.Aft && box.HalfWidth > 0, "an empty box");
            Assert.InRange(box.Aft, 0, hull.Length);
            Assert.InRange(box.Fore, 0, hull.Length);
            Assert.True(box.HalfWidth <= hull.Beam / 2, "wider than the ship");
        });
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void MaxHeightAboveDeck_IsTheTopOfTheHighestBox(int hullId)
    {
        Hull hull = Seeds.GetById(hullId)!;
        double highest = SuperstructureBoxes.For(hull).Max(box => box.Top);

        Assert.Equal(hull.Depth * (1 + SuperstructureBoxes.MaxHeightAboveDeck(hull.Shape)), highest, Tolerance);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    public void TheMesh_IsBuiltFromTheSameBoxes(int hullId)
    {
        // One definition for both views: the 3D superstructure spans exactly the boxes' extent.
        Hull hull = Seeds.GetById(hullId)!;
        IReadOnlyList<SuperstructureBox> boxes = SuperstructureBoxes.For(hull);
        Vector3[] vertices = ShipMeshBuilder.Build(hull, draft: null).Quads
            .Where(q => q.Part is not (MeshPart.Hull or MeshPart.Deck))
            .SelectMany(q => new[] { q.A, q.B, q.C, q.D })
            .ToArray();

        Assert.Equal(boxes.Min(b => b.Aft), vertices.Min(v => v.X), Tolerance);
        Assert.Equal(boxes.Max(b => b.Fore), vertices.Max(v => v.X), Tolerance);
        Assert.Equal(boxes.Max(b => b.Top), vertices.Max(v => v.Z), Tolerance);
        Assert.Equal(5 * boxes.Count, vertices.Length / 4);  // four walls and a roof per box
    }

    [Fact]
    public void TheSov_HasItsAccommodationForward_AndTheFeeder_Aft()
    {
        Hull sov = Seeds.GetById(6)!, feeder = Seeds.GetById(5)!;

        Assert.All(SuperstructureBoxes.For(sov).Where(b => b.Part == MeshPart.Superstructure), b => Assert.True(b.Aft > sov.Length / 2));
        Assert.All(SuperstructureBoxes.For(feeder).Where(b => b.Part == MeshPart.Superstructure), b => Assert.True(b.Fore < feeder.Length / 2));
    }
}
