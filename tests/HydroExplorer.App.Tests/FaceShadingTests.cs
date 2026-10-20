using System.Numerics;
using HydroExplorer.App.Rendering;

namespace HydroExplorer.App.Tests;

public class FaceShadingTests
{
    private const float Tolerance = 1e-6f;

    // A 2 × 2 m square in the deck plane (z = 0), anticlockwise seen from above, so its normal points up.
    private static readonly MeshQuad UpFacing = new(
        new Vector3(0, 0, 0), new Vector3(2, 0, 0), new Vector3(2, 2, 0), new Vector3(0, 2, 0),
        MeshPart.Deck, IsBelowWater: false);

    [Fact]
    public void Normal_OfAnAnticlockwiseQuad_PointsTowardsTheViewer()
    {
        Vector3 normal = FaceShading.Normal(UpFacing);

        Assert.Equal(0, normal.X, Tolerance);
        Assert.Equal(0, normal.Y, Tolerance);
        Assert.Equal(1, normal.Z, Tolerance);
    }

    [Fact]
    public void Normal_OfACollapsedQuad_IsZero_NotNaN()
    {
        // At a ship's bow both sides meet, so some quads have no area. They must not poison the shading with NaN.
        var line = new MeshQuad(Vector3.Zero, Vector3.UnitX, Vector3.UnitX, Vector3.Zero, MeshPart.Hull, false);

        Assert.Equal(Vector3.Zero, FaceShading.Normal(line));
    }

    [Fact]
    public void Centroid_IsTheAverageOfTheCorners()
    {
        Assert.Equal(new Vector3(1, 1, 0), UpFacing.Centroid);
    }

    [Fact]
    public void IsFacingCamera_TrueFromAbove_FalseFromBelow()
    {
        Assert.True(FaceShading.IsFacingCamera(UpFacing, eye: new Vector3(1, 1, 10)));
        Assert.False(FaceShading.IsFacingCamera(UpFacing, eye: new Vector3(1, 1, -10)));
    }

    [Fact]
    public void Brightness_IsFullFacingTheLight_AndAmbientFacingAway()
    {
        Vector3 up = Vector3.UnitZ;

        Assert.Equal(FaceShading.Ambient + FaceShading.Diffuse, FaceShading.Brightness(up, towardLight: up), Tolerance);
        Assert.Equal(FaceShading.Ambient, FaceShading.Brightness(up, towardLight: -up), Tolerance);       // lit from behind
        Assert.Equal(FaceShading.Ambient, FaceShading.Brightness(up, towardLight: Vector3.UnitX), Tolerance);  // grazing
    }

    [Fact]
    public void Brightness_FollowsLambertsCosineLaw()
    {
        // Light 60° away from the normal: cos 60° = 0.5.
        var towardLight = new Vector3(MathF.Sin(MathF.PI / 3), 0, MathF.Cos(MathF.PI / 3));

        Assert.Equal(FaceShading.Ambient + FaceShading.Diffuse * 0.5f, FaceShading.Brightness(Vector3.UnitZ, towardLight), Tolerance);
    }

    [Fact]
    public void Headlight_LightsTheSideTheCameraSees_FromAbove()
    {
        var eye = new Vector3(0, -100, 20);  // out to starboard, a little above
        Vector3 light = FaceShading.Headlight(eye, target: Vector3.Zero);

        Assert.Equal(1, light.Length(), Tolerance);
        Assert.True(FaceShading.Brightness(-Vector3.UnitY, light) > 0.8f);  // starboard side, facing the camera
        Assert.Equal(FaceShading.Ambient, FaceShading.Brightness(Vector3.UnitY, light), Tolerance);  // far side
        Assert.True(light.Z > 0);  // from above
    }
}
