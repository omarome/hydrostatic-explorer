using HydroExplorer.Core.Paging;

namespace HydroExplorer.Core.Tests;

public class PagerTests
{
    // 1191 items = the Large Pontoon table size. 1191 / 25 = 47 full pages + 16 items on page 48.
    private static readonly IReadOnlyList<int> Items = Enumerable.Range(1, 1191).ToList();

    [Fact]
    public void Paginate_FirstPage_ReturnsFirstItemsAndTotals()
    {
        PagedResult<int> result = Pager.Paginate(Items, page: 1, pageSize: 25);

        Assert.Equal(Enumerable.Range(1, 25), result.Items);
        Assert.Equal(1, result.Page);
        Assert.Equal(25, result.PageSize);
        Assert.Equal(1191, result.TotalCount);
        Assert.Equal(48, result.TotalPages);
    }

    [Fact]
    public void Paginate_MiddlePage_ReturnsCorrectSlice()
    {
        PagedResult<int> result = Pager.Paginate(Items, page: 3, pageSize: 25);

        Assert.Equal(51, result.Items[0]);
        Assert.Equal(75, result.Items[^1]);
    }

    [Fact]
    public void Paginate_LastPartialPage_ReturnsRemainingItems()
    {
        PagedResult<int> result = Pager.Paginate(Items, page: 48, pageSize: 25);

        Assert.Equal(16, result.Items.Count);
        Assert.Equal(1176, result.Items[0]);
        Assert.Equal(1191, result.Items[^1]);
    }

    [Fact]
    public void Paginate_PagePastTheEnd_ReturnsNoItemsButCorrectTotals()
    {
        PagedResult<int> result = Pager.Paginate(Items, page: 49, pageSize: 25);

        Assert.Empty(result.Items);
        Assert.Equal(49, result.Page);
        Assert.Equal(1191, result.TotalCount);
        Assert.Equal(48, result.TotalPages);
    }

    [Fact]
    public void Paginate_EmptySource_HasZeroPages()
    {
        PagedResult<int> result = Pager.Paginate(Array.Empty<int>(), page: 1, pageSize: 25);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(-1, 25)]
    [InlineData(1, 0)]
    public void Paginate_InvalidPageOrPageSize_Throws(int page, int pageSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Pager.Paginate(Items, page, pageSize));
    }

    [Theory]
    [InlineData(0, 25, 0)]
    [InlineData(1, 25, 1)]
    [InlineData(25, 25, 1)]
    [InlineData(26, 25, 2)]
    [InlineData(1191, 25, 48)]
    public void TotalPages_RoundsUp(int totalCount, int pageSize, int expected)
    {
        Assert.Equal(expected, Pager.TotalPages(totalCount, pageSize));
    }

    [Theory]
    [InlineData(0, 25, 1)]
    [InlineData(24, 25, 1)]
    [InlineData(25, 25, 2)]
    [InlineData(315, 25, 13)]  // draft 3.25 m is index 315 in a table starting at 0.10 m
    public void PageOfIndex_ReturnsOneBasedPage(int index, int pageSize, int expected)
    {
        Assert.Equal(expected, Pager.PageOfIndex(index, pageSize));
    }
}
