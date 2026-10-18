using HydroExplorer.Core.Data;
using HydroExplorer.Core.Models;

namespace HydroExplorer.Core.Tests;

public class SeedHullRepositoryTests
{
    private readonly SeedHullRepository _repository = new();

    [Fact]
    public void GetAll_ReturnsTheThreeSampleHulls()
    {
        IReadOnlyList<Hull> hulls = _repository.GetAll();

        Assert.Equal(new[] { 1, 2, 3 }, hulls.Select(h => h.Id));
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
