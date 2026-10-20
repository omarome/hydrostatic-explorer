using System.Numerics;
using Avalonia;
using HydroExplorer.App.Rendering;
using HydroExplorer.Core.Data;
using HydroExplorer.Core.Models;

namespace HydroExplorer.App.Tests;

public class DrawOrderTests
{
    private static readonly Hull Feeder = new SeedHullRepository().GetById(5)!;
    private static readonly ShipMesh Mesh = ShipMeshBuilder.Build(Feeder, draft: 5.5);

    private static OrbitCamera Camera(float yaw, float pitch)
    {
        var camera = new OrbitCamera();
        camera.FitTo(Mesh.Min, Mesh.Max, new Size(800, 500));
        camera.YawDegrees = yaw;
        camera.PitchDegrees = pitch;
        return camera;
    }

    [Theory]
    [InlineData(30, 25)]
    [InlineData(200, 60)]
    public void Update_KeepsExactlyTheFacesTurnedTowardsTheCamera(float yaw, float pitch)
    {
        OrbitCamera camera = Camera(yaw, pitch);
        var order = new DrawOrder();

        order.Update(Mesh, camera);

        int facing = Mesh.Quads.Count(q => FaceShading.IsFacingCamera(q, camera.Eye));
        Assert.Equal(facing, order.BelowWater.Count + order.AboveWater.Count);
        Assert.All(order.BelowWater.Concat(order.AboveWater), q => Assert.True(FaceShading.IsFacingCamera(q, camera.Eye)));
    }

    [Fact]
    public void Update_SplitsTheFacesIntoTheTwoPasses()
    {
        var order = new DrawOrder();

        order.Update(Mesh, Camera(30, 25));

        Assert.NotEmpty(order.BelowWater);
        Assert.NotEmpty(order.AboveWater);
        Assert.All(order.BelowWater, q => Assert.True(q.IsBelowWater));
        Assert.All(order.AboveWater, q => Assert.False(q.IsBelowWater));
    }

    [Theory]
    [InlineData(30, 25)]
    [InlineData(135, 10)]
    [InlineData(300, 80)]
    public void Update_SortsEachPassFromFarToNear(float yaw, float pitch)
    {
        // The painter's algorithm: draw the farthest face first, so nearer faces paint over it.
        OrbitCamera camera = Camera(yaw, pitch);
        var order = new DrawOrder();

        order.Update(Mesh, camera);

        AssertFarToNear(order.BelowWater, camera);
        AssertFarToNear(order.AboveWater, camera);
    }

    [Fact]
    public void Update_CanBeCalledAgain_ReplacingThePreviousResult()
    {
        var order = new DrawOrder();
        order.Update(Mesh, Camera(30, 25));
        int firstCount = order.BelowWater.Count + order.AboveWater.Count;

        order.Update(Mesh, Camera(30, 25));

        Assert.Equal(firstCount, order.BelowWater.Count + order.AboveWater.Count);
    }

    private static void AssertFarToNear(IReadOnlyList<MeshQuad> faces, OrbitCamera camera)
    {
        for (int i = 1; i < faces.Count; i++)
        {
            Assert.True(camera.ViewDepth(faces[i - 1].Centroid) >= camera.ViewDepth(faces[i].Centroid) - 1e-3f);
        }
    }
}
