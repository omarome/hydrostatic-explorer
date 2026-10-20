using System.Numerics;
using Avalonia;
using HydroExplorer.App.Rendering;
using HydroExplorer.Core.Data;
using HydroExplorer.Core.Models;

namespace HydroExplorer.App.Tests;

public class OrbitCameraTests
{
    private const double PixelTolerance = 1e-3;
    private static readonly Size Viewport = new(800, 500);

    private static OrbitCamera CameraAtOrigin(float yaw = 0, float pitch = 30) => new()
    {
        Target = Vector3.Zero,
        Distance = 50,
        YawDegrees = yaw,
        PitchDegrees = pitch,
        Viewport = Viewport,
    };

    [Fact]
    public void Project_TheTarget_LandsInTheViewportCentre()
    {
        var camera = CameraAtOrigin(yaw: 37, pitch: 20);
        camera.Target = new Vector3(10, -4, 3);

        Point centre = camera.Project(camera.Target)!.Value;

        Assert.Equal(400, centre.X, PixelTolerance);
        Assert.Equal(250, centre.Y, PixelTolerance);
    }

    [Fact]
    public void AtYaw0_TheBowIsToTheRight_AndUpIsUp()
    {
        // Yaw 0 looks at the starboard side: +x (towards the bow) goes right, +z goes up the screen.
        var camera = CameraAtOrigin(pitch: 5);

        Assert.True(camera.Project(new Vector3(5, 0, 0))!.Value.X > 400);
        Assert.True(camera.Project(new Vector3(0, 0, 5))!.Value.Y < 250);
    }

    [Fact]
    public void TurningTheYawBy90Degrees_RotatesTheViewAboutTheVerticalAxis()
    {
        // Turning the camera +90° about z is the same as turning the world −90°: the point that is to the right
        // at yaw 90 (+y) is where +x was at yaw 0, and +x (now pointing at the camera) is where −y was.
        OrbitCamera yaw0 = CameraAtOrigin(yaw: 0);
        OrbitCamera yaw90 = CameraAtOrigin(yaw: 90);

        AssertSamePixel(yaw0.Project(new Vector3(5, 0, 0)), yaw90.Project(new Vector3(0, 5, 0)));
        AssertSamePixel(yaw0.Project(new Vector3(0, -5, 0)), yaw90.Project(new Vector3(5, 0, 0)));
    }

    [Fact]
    public void Project_APointBehindTheCamera_ReturnsNull()
    {
        var camera = CameraAtOrigin();
        Vector3 behind = camera.Eye + (camera.Eye - camera.Target) * 0.5f;

        Assert.Null(camera.Project(behind));
    }

    [Theory]
    [InlineData(100, 85)]
    [InlineData(-20, 5)]
    [InlineData(45, 45)]
    public void Pitch_IsClampedSoTheCameraStaysAboveTheWater(float requested, float expected)
    {
        var camera = new OrbitCamera { PitchDegrees = requested };

        Assert.Equal(expected, camera.PitchDegrees);
    }

    [Fact]
    public void ViewDepth_IsLargerForFartherPoints()
    {
        var camera = CameraAtOrigin();
        Vector3 towardCamera = Vector3.Normalize(camera.Eye - camera.Target);

        Assert.True(camera.ViewDepth(-towardCamera * 5) > camera.ViewDepth(towardCamera * 5));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(5, 0)]
    [InlineData(5, 135)]
    [InlineData(3, 270)]
    public void FitTo_KeepsEveryMeshVertexInsideTheViewport_FromAnyAngle(int hullId, float yaw)
    {
        Hull hull = new SeedHullRepository().GetById(hullId)!;
        ShipMesh mesh = ShipMeshBuilder.Build(hull, draft: hull.Depth / 2);
        var camera = new OrbitCamera();

        // Fit once, then turn: the fit uses the bounding sphere, so it holds from every direction.
        camera.FitTo(mesh.Min, mesh.Max, Viewport);
        camera.YawDegrees = yaw;
        camera.PitchDegrees = 60;

        foreach (Vector3 vertex in mesh.Quads.SelectMany(q => new[] { q.A, q.B, q.C, q.D }))
        {
            Point p = camera.Project(vertex) ?? throw new Xunit.Sdk.XunitException($"{vertex} is behind the camera");
            Assert.InRange(p.X, 0, Viewport.Width);
            Assert.InRange(p.Y, 0, Viewport.Height);
        }
    }

    private static void AssertSamePixel(Point? expected, Point? actual)
    {
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected.Value.X, actual.Value.X, PixelTolerance);
        Assert.Equal(expected.Value.Y, actual.Value.Y, PixelTolerance);
    }
}
