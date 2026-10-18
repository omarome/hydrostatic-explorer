using HydroExplorer.App.Services;
using HydroExplorer.App.ViewModels;
using HydroExplorer.Core.Models;

namespace HydroExplorer.App.Tests;

public class HullViewModelTests
{
    private static readonly Hull CoastalBarge = new(2, "Coastal Barge", 80, 16, 7);

    private readonly SelectionService _selection = new();
    private readonly HullViewModel _vm;

    public HullViewModelTests()
    {
        _vm = new HullViewModel(_selection);
    }

    [Fact]
    public void WithNothingSelected_HasNoHullAndNoWaterline()
    {
        Assert.Equal(0, _vm.Length);
        Assert.Equal(0, _vm.Depth);
        Assert.Null(_vm.Draft);
    }

    [Fact]
    public void SelectingAHull_ShowsItsDimensions_WithoutWaterline()
    {
        _selection.SelectedHull = CoastalBarge;

        Assert.Equal("Coastal Barge", _vm.HullName);
        Assert.Equal(80, _vm.Length);
        Assert.Equal(7, _vm.Depth);
        Assert.Null(_vm.Draft);
    }

    [Fact]
    public void SelectingARow_MovesTheWaterlineToItsDraft()
    {
        _selection.SelectedHull = CoastalBarge;

        _selection.SelectDraft(3.25);

        Assert.Equal(3.25, _vm.Draft);
        Assert.Equal(3.25, _vm.SliderDraft);
    }

    [Fact]
    public void ChangingHull_RemovesTheWaterline()
    {
        _selection.SelectedHull = CoastalBarge;
        _selection.SelectDraft(3.25);

        _selection.SelectedHull = new Hull(1, "Small Barge", 40, 10, 4);

        Assert.Equal(40, _vm.Length);
        Assert.Null(_vm.Draft);
    }

    [Fact]
    public void MovingTheSlider_SnapsToWholeCentimetres_WithinTheHull()
    {
        _selection.SelectedHull = CoastalBarge;

        _vm.SliderDraft = 3.2547;
        Assert.Equal(3.25, _selection.SelectedDraft);

        _vm.SliderDraft = 50;
        Assert.Equal(7.0, _selection.SelectedDraft);
    }

    [Fact]
    public void ChangingHull_DoesNotSelectADraft_EvenThoughTheSliderResets()
    {
        _selection.SelectedHull = CoastalBarge;
        _selection.SelectDraft(5);

        _selection.SelectedHull = new Hull(1, "Small Barge", 40, 10, 4);

        Assert.Null(_selection.SelectedDraft);
        Assert.Equal("T = –", _vm.DraftLabel);
    }
}
