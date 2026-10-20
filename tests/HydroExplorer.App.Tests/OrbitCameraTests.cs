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

    // ---- Framing the default view, zoom limits ----

    private static (Hull Hull, Vector3[] Points) FeederPoints()
    {
        Hull feeder = new SeedHullRepository().GetById(5)!;
        ShipMesh mesh = ShipMeshBuilder.Build(feeder, draft: 5.5);
        Vector3[] points = mesh.Quads.SelectMany(q => new[] { q.A, q.B, q.C, q.D }).ToArray();
        return (feeder, points);
    }

    [Fact]
    public void Frame_FillsMostOfTheViewport_WithEveryPointInside()
    {
        (Hull feeder, Vector3[] points) = FeederPoints();
        var camera = new OrbitCamera { YawDegrees = 30, PitchDegrees = 25 };

        camera.Frame(points, target: new Vector3(70, 0, (float)feeder.Depth), Viewport);

        Point[] projected = points.Select(p => camera.Project(p)!.Value).ToArray();
        Assert.All(projected, p => Assert.InRange(p.X, 0, Viewport.Width));
        Assert.All(projected, p => Assert.InRange(p.Y, 0, Viewport.Height));

        // "Fills": the model reaches at least 80 % of the way to the nearer viewport edge.
        double reach = projected.Max(p => Math.Max(
            Math.Abs(p.X - Viewport.Width / 2) / (Viewport.Width / 2),
            Math.Abs(p.Y - Viewport.Height / 2) / (Viewport.Height / 2)));
        Assert.InRange(reach, 0.8, 1.0);
    }

    [Fact]
    public void Frame_IsTighterThanTheBoundingSphereFit()
    {
        (Hull feeder, Vector3[] points) = FeederPoints();
        var sphere = new OrbitCamera { YawDegrees = 30, PitchDegrees = 25 };
        var framed = new OrbitCamera { YawDegrees = 30, PitchDegrees = 25 };

        sphere.FitTo(points.Aggregate(Vector3.Min), points.Aggregate(Vector3.Max), Viewport);
        framed.Frame(points, target: sphere.Target, Viewport);

        Assert.True(framed.Distance < sphere.Distance);
    }

    [Fact]
    public void Zoom_IsLimited_SoTheCameraNeverEntersTheModel()
    {
        (Hull feeder, Vector3[] points) = FeederPoints();
        var target = new Vector3(70, 0, (float)feeder.Depth);
        var camera = new OrbitCamera();
        camera.Frame(points, target, Viewport);
        float radius = points.Max(p => Vector3.Distance(p, target));

        camera.Zoom(0.001f);  // try to zoom right in
        Assert.True(camera.Distance > radius);
        Assert.All(points, p => Assert.NotNull(camera.Project(p)));  // nothing ends up behind the camera

        camera.Zoom(1000);    // and right out
        Assert.True(camera.Distance <= radius * 10);
    }

    [Theory]
    [InlineData(5, 0)]
    [InlineData(5, 180)]
    [InlineData(85, 90)]
    public void CameraAimedAtTheDeck_IsAlwaysAboveAnyWaterline(float pitch, float yaw)
    {
        // Why three drawing passes (below water, water, above water) are enough: with the target on the deck
        // and pitch ≥ 5°, the eye is above z = D ≥ T, so the water plane always hides the underwater faces
        // and never anything above it.
        (Hull feeder, Vector3[] points) = FeederPoints();
        var camera = new OrbitCamera { YawDegrees = yaw, PitchDegrees = pitch };
        camera.Frame(points, target: new Vector3(70, 0, (float)feeder.Depth), Viewport);
        camera.Zoom(0.001f);

        Assert.True(camera.Eye.Z > feeder.Depth);
    }

    private static void AssertSamePixel(Point? expected, Point? actual)
    {
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected.Value.X, actual.Value.X, PixelTolerance);
        Assert.Equal(expected.Value.Y, actual.Value.Y, PixelTolerance);
    }
}
