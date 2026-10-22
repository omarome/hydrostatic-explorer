using HydroExplorer.Core.Data;
using HydroExplorer.Core.Models;

namespace HydroExplorer.Core.Tests;

public class SeedHullRepositoryTests
{
    private readonly SeedHullRepository _repository = new();

    [Fact]
    public void GetAll_ReturnsTheSixSampleHulls()
    {
        IReadOnlyList<Hull> hulls = _repository.GetAll();

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, hulls.Select(h => h.Id));
    }

    [Fact]
    public void Hulls1To3_AreBoxes_And4To6_AreShips()
    {
        // Other tests rely on this split (box formulas vs integration).
        Assert.All(_repository.GetAll().Where(h => h.Id <= 3), h => Assert.Null(h.Shape));
        Assert.All(_repository.GetAll().Where(h => h.Id >= 4), h => Assert.NotNull(h.Shape));
    }

    [Theory]
    [InlineData(4, 0.79)]  // Coastal Tanker: full ends and flat bottom
    [InlineData(5, 0.63)]  // Container Feeder: finer
    [InlineData(6, 0.70)]  // Service Operation Vessel: full stern for the aft deck
    public void ShipHulls_HaveThePlannedBlockCoefficient(int id, double cb)
    {
        Assert.Equal(cb, _repository.GetById(id)!.Shape!.FullDepthBlockCoefficient, tolerance: 0.01);
    }

    [Fact]
    public void CargoShips_HaveAccommodationAft_AndTheSov_Forward()
    {
        Assert.Equal(SuperstructureLayout.AftAccommodation, _repository.GetById(4)!.Shape!.Superstructure);
        Assert.Equal(SuperstructureLayout.AftAccommodation, _repository.GetById(5)!.Shape!.Superstructure);
        Assert.Equal(SuperstructureLayout.ForwardAccommodation, _repository.GetById(6)!.Shape!.Superstructure);
    }

    [Fact]
    public void HullShape_DefaultsToAftAccommodation()
    {
        Assert.Equal(SuperstructureLayout.AftAccommodation, new HullShape(2, 2, 2).Superstructure);
    }

    [Fact]
    public void GetById_KnownId_ReturnsHull()
    {
        Assert.Equal(new Hull(2, "Coastal Barge", 80, 16, 7), _repository.GetById(2));
    }

    [Fact]
    public void GetById_UnknownId_ReturnsNull()
    {
        Assert.Null(_repository.GetById(99));
    }
}
