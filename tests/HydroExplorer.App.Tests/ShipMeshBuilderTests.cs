using System.Numerics;
using HydroExplorer.App.Rendering;
using HydroExplorer.Core.Data;
using HydroExplorer.Core.Models;

namespace HydroExplorer.App.Tests;

public class ShipMeshBuilderTests
{
    private const float Tolerance = 1e-4f;  // metres; the mesh is stored as float

    private static readonly SeedHullRepository Seeds = new();
    private static readonly Hull Barge = Seeds.GetById(2)!;   // Coastal Barge, a box: 80 × 16 × 7
    private static readonly Hull Feeder = Seeds.GetById(5)!;  // Container Feeder, a ship: 140 × 22 × 11

    private static IEnumerable<Vector3> Vertices(ShipMesh mesh) => mesh.Quads.SelectMany(q => new[] { q.A, q.B, q.C, q.D });

    private static IEnumerable<MeshQuad> Part(ShipMesh mesh, MeshPart part) => mesh.Quads.Where(q => q.Part == part);

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    public void EveryVertex_IsInsideTheHullBounds_PlusTheSuperstructure(int hullId)
    {
        Hull hull = Seeds.GetById(hullId)!;
        ShipMesh mesh = ShipMeshBuilder.Build(hull, draft: hull.Depth / 3);
        float top = (float)(hull.Depth * (1 + ShipMeshBuilder.SuperstructureHeightFactor));

        foreach (Vector3 v in Vertices(mesh))
        {
            Assert.InRange(v.X, -Tolerance, hull.Length + Tolerance);
            Assert.InRange(MathF.Abs(v.Y), 0, hull.Beam / 2 + Tolerance);
            Assert.InRange(v.Z, -Tolerance, top + Tolerance);
        }
    }

    [Fact]
    public void MinAndMax_AreTheMeshBounds()
    {
        ShipMesh mesh = ShipMeshBuilder.Build(Feeder, draft: 5);

        Assert.Equal(Vertices(mesh).Aggregate(Vector3.Min), mesh.Min);
        Assert.Equal(Vertices(mesh).Aggregate(Vector3.Max), mesh.Max);
    }

    [Theory]
    [InlineData(5.5)]
    [InlineData(3.27)]
    public void HullSides_HaveARowOfVerticesExactlyAtTheDraft(double draft)
    {
        ShipMesh mesh = ShipMeshBuilder.Build(Feeder, draft);

        Assert.Contains(Vertices(mesh), v => v.Z == (float)draft);
    }

    [Theory]
    [InlineData(2, 3.27)]
    [InlineData(5, 5.5)]
    public void HullFaces_SplitAtTheWaterline(int hullId, double draft)
    {
        ShipMesh mesh = ShipMeshBuilder.Build(Seeds.GetById(hullId)!, draft);
        float t = (float)draft;

        foreach (MeshQuad quad in Part(mesh, MeshPart.Hull))
        {
            float[] z = { quad.A.Z, quad.B.Z, quad.C.Z, quad.D.Z };
            if (quad.IsBelowWater)
            {
                Assert.True(z.Max() <= t, $"Below-water face reaches up to {z.Max()} m, above T = {t} m");
            }
            else
            {
                Assert.True(z.Min() >= t, $"Above-water face reaches down to {z.Min()} m, below T = {t} m");
            }
        }

        Assert.Contains(mesh.Quads, q => q.IsBelowWater);
        Assert.Contains(mesh.Quads, q => !q.IsBelowWater);
    }

    [Fact]
    public void WithoutADraft_NothingIsBelowWater()
    {
        Assert.DoesNotContain(ShipMeshBuilder.Build(Feeder, draft: null).Quads, q => q.IsBelowWater);
    }

    [Fact]
    public void OnlyHullFaces_CanBeBelowWater()
    {
        ShipMesh mesh = ShipMeshBuilder.Build(Feeder, draft: Feeder.Depth);  // even with the deck awash

        Assert.All(mesh.Quads.Where(q => q.IsBelowWater), q => Assert.Equal(MeshPart.Hull, q.Part));
    }

    [Fact]
    public void BoxHull_HasFlatSides()
    {
        ShipMesh mesh = ShipMeshBuilder.Build(Barge, draft: 3);

        // Side faces (normal across the ship) lie exactly at ±B/2, and nothing is wider.
        IEnumerable<MeshQuad> sides = Part(mesh, MeshPart.Hull).Where(q => MathF.Abs(FaceShading.Normal(q).Y) > 0.99f);
        Assert.NotEmpty(sides);
        Assert.All(sides.SelectMany(q => new[] { q.A, q.B, q.C, q.D }), v => Assert.Equal(8, MathF.Abs(v.Y), Tolerance));
    }

    [Fact]
    public void BoxHull_IsClosed_WithABottomAndBothEnds()
    {
        ShipMesh mesh = ShipMeshBuilder.Build(Barge, draft: 3);
        Vector3[] normals = Part(mesh, MeshPart.Hull).Select(q => FaceShading.Normal(q)).ToArray();

        Assert.Contains(normals, n => n.Z < -0.99f);  // bottom
        Assert.Contains(normals, n => n.X < -0.99f);  // stern (transom)
        Assert.Contains(normals, n => n.X > 0.99f);   // bow
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public void HullFaces_PointOutwards(int hullId)
    {
        // Culling and shading depend on outward normals: a face's normal must point away from the centreline
        // (or down, for a box's bottom, or along the ship, for a box's ends).
        Hull hull = Seeds.GetById(hullId)!;
        ShipMesh mesh = ShipMeshBuilder.Build(hull, draft: hull.Depth / 2);
        var centre = new Vector3((float)hull.Length / 2, 0, (float)hull.Depth / 2);

        foreach (MeshQuad quad in Part(mesh, MeshPart.Hull).Concat(Part(mesh, MeshPart.Deck)))
        {
            Vector3 normal = FaceShading.Normal(quad);
            if (normal != Vector3.Zero)
            {
                Assert.True(Vector3.Dot(normal, quad.Centroid - centre) > 0, $"Face at {quad.Centroid} points inwards");
            }
        }
    }

    [Fact]
    public void Superstructure_IsOnlyBuiltForShipHulls()
    {
        Assert.DoesNotContain(ShipMeshBuilder.Build(Barge, 3).Quads, q => q.Part is MeshPart.Superstructure or MeshPart.Funnel);

        ShipMesh ship = ShipMeshBuilder.Build(Feeder, 5);
        Assert.Contains(ship.Quads, q => q.Part == MeshPart.Superstructure);
        Assert.Contains(ship.Quads, q => q.Part == MeshPart.Funnel);
    }

    [Fact]
    public void Superstructure_SitsOnTheDeck_InTheAftPart()
    {
        ShipMesh mesh = ShipMeshBuilder.Build(Feeder, 5);
        Vector3[] vertices = mesh.Quads
            .Where(q => q.Part is MeshPart.Superstructure or MeshPart.Funnel)
            .SelectMany(q => new[] { q.A, q.B, q.C, q.D })
            .ToArray();

        Assert.All(vertices, v => Assert.True(v.Z >= Feeder.Depth - Tolerance, "Superstructure below the deck"));
        Assert.All(vertices, v => Assert.InRange(v.X, 0.04f * 140 - Tolerance, 0.20f * 140 + Tolerance));
    }

    [Fact]
    public void WaterPlane_IsAtTheDraft_AndReachesPastTheHull()
    {
        Vector3[] corners = ShipMeshBuilder.WaterPlane(Feeder, draft: 5.5);

        Assert.Equal(4, corners.Length);
        Assert.All(corners, c => Assert.Equal(5.5f, c.Z));
        Assert.True(corners.Min(c => c.X) < 0 && corners.Max(c => c.X) > Feeder.Length);
        Assert.True(corners.Min(c => c.Y) < -Feeder.Beam / 2 && corners.Max(c => c.Y) > Feeder.Beam / 2);
    }

    [Fact]
    public void Waterline_FollowsTheHullAtTheDraft_OnBothSides()
    {
        Vector3[] loop = ShipMeshBuilder.Waterline(Feeder, draft: 5.5);

        Assert.All(loop, p => Assert.Equal(5.5f, p.Z));
        Assert.All(loop, p => Assert.Equal(
            Core.Hydrostatics.HullGeometry.HalfBreadth(Feeder, p.X, 5.5), MathF.Abs(p.Y), Tolerance));
        Assert.Contains(loop, p => p.Y < -1);  // starboard
        Assert.Contains(loop, p => p.Y > 1);   // port
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(11.5)]
    public void Build_RejectsADraftOutsideTheHull(double draft)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ShipMeshBuilder.Build(Feeder, draft));
    }
}
