using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HydroExplorer.Core.Data;
using HydroExplorer.Core.Hydrostatics;
using HydroExplorer.Core.Models;
using HydroExplorer.Core.Paging;
using Microsoft.AspNetCore.Http;

namespace HydroExplorer.Api.Tests;

/// <summary>
/// End-to-end tests of the HTTP API: real routing, model binding, validation and JSON.
/// IClassFixture shares one ApiFactory (one in-memory server) across all tests in this class.
/// </summary>
public class HydrostaticsEndpointTests : IClassFixture<ApiFactory>
{
    private const string LargePontoon = "/api/hulls/3/hydrostatics";
    private readonly HttpClient _client;

    public HydrostaticsEndpointTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ---- GET /api/hulls ----

    [Fact]
    public async Task GetHulls_ReturnsSeedHulls()
    {
        var hulls = await _client.GetFromJsonAsync<List<Hull>>("/api/hulls");

        Assert.Equal(new SeedHullRepository().GetAll(), hulls);
    }

    [Fact]
    public async Task GetHulls_IncludesTheShape_NullForBoxes()
    {
        // Check the raw JSON, so the property names the app and curl users see are pinned down too.
        using JsonDocument json = JsonDocument.Parse(await _client.GetStringAsync("/api/hulls"));
        JsonElement[] hulls = json.RootElement.EnumerateArray().ToArray();

        Assert.Equal(JsonValueKind.Null, hulls[0].GetProperty("shape").ValueKind);  // 1, Small Barge
        JsonElement feeder = hulls.Single(h => h.GetProperty("id").GetInt32() == 5).GetProperty("shape");
        Assert.Equal(3, feeder.GetProperty("bowExponent").GetDouble());
        Assert.Equal(3, feeder.GetProperty("sternExponent").GetDouble());
        Assert.Equal(5, feeder.GetProperty("sectionExponent").GetDouble());
    }

    [Fact]
    public async Task GetHydrostatics_ShipHull_ReturnsIntegratedValues()
    {
        Hull feeder = new SeedHullRepository().GetById(5)!;  // Container Feeder: 140 × 22 × 11 m, 1,091 rows

        PagedResult<HydrostaticRow> page = await GetPage("/api/hulls/5/hydrostatics?minDraft=5.5&maxDraft=5.5");
        var curve = await _client.GetFromJsonAsync<List<CurvePoint>>("/api/hulls/5/hydrostatics/curve?points=10");

        Assert.Equal(1, page.TotalCount);
        Assert.Equal(HydrostaticsCalculator.Calculate(feeder, 5.5), page.Items[0]);
        Assert.True(page.Items[0].Volume < feeder.Length * feeder.Beam * 5.5);  // finer than a box
        Assert.Equal(10, curve!.Count);
    }

    // ---- Paging ----

    [Fact]
    public async Task GetHydrostatics_Defaults_ReturnsFirstPageOf25SortedByDraft()
    {
        PagedResult<HydrostaticRow> result = await GetPage(LargePontoon);

        Assert.Equal(1, result.Page);
        Assert.Equal(25, result.PageSize);
        Assert.Equal(1191, result.TotalCount);
        Assert.Equal(48, result.TotalPages);
        Assert.Equal(25, result.Items.Count);
        Assert.Equal(0.10, result.Items[0].Draft);
        Assert.Equal(0.34, result.Items[^1].Draft);
    }

    [Fact]
    public async Task GetHydrostatics_LastPage_IsPartial()
    {
        PagedResult<HydrostaticRow> result = await GetPage($"{LargePontoon}?page=48");

        Assert.Equal(16, result.Items.Count);
        Assert.Equal(11.85, result.Items[0].Draft);
        Assert.Equal(12.00, result.Items[^1].Draft);
    }

    [Fact]
    public async Task GetHydrostatics_PagePastTheEnd_ReturnsEmptyItems()
    {
        PagedResult<HydrostaticRow> result = await GetPage($"{LargePontoon}?page=49");

        Assert.Empty(result.Items);
        Assert.Equal(48, result.TotalPages);
    }

    [Fact]
    public async Task GetHydrostatics_MaxPageSize_IsAccepted()
    {
        PagedResult<HydrostaticRow> result = await GetPage($"{LargePontoon}?pageSize=200");

        Assert.Equal(200, result.Items.Count);
        Assert.Equal(6, result.TotalPages);  // 1191 / 200, rounded up
    }

    [Fact]
    public async Task GetHydrostatics_RowValues_MatchCalculator()
    {
        PagedResult<HydrostaticRow> result = await GetPage("/api/hulls/2/hydrostatics?minDraft=4&maxDraft=4");

        Hull coastalBarge = new SeedHullRepository().GetById(2)!;
        Assert.Equal(HydrostaticsCalculator.Calculate(coastalBarge, 4.0), Assert.Single(result.Items));
    }

    // ---- Sorting ----

    [Fact]
    public async Task GetHydrostatics_SortDraftDescending_StartsAtDepth()
    {
        PagedResult<HydrostaticRow> result = await GetPage($"{LargePontoon}?sortBy=draft&sortDir=desc");

        Assert.Equal(12.00, result.Items[0].Draft);
        Assert.Equal(11.99, result.Items[1].Draft);
    }

    [Fact]
    public async Task GetHydrostatics_SortByKm_OrdersWholeTableByKm()
    {
        // KM = T/2 + B²/(12T) is smallest at T = B/√6. For the Coastal Barge (B = 16, D = 7) that is
        // 6.53 m, inside the table, so the KM order differs from both draft orders. The smallest KM
        // being first proves the whole table is sorted before paging.
        PagedResult<HydrostaticRow> result = await GetPage("/api/hulls/2/hydrostatics?sortBy=km&sortDir=asc&pageSize=200");

        double smallestKmInTable = HydrostaticTableGenerator.Generate(new SeedHullRepository().GetById(2)!).Min(r => r.Km);
        Assert.Equal(smallestKmInTable, result.Items[0].Km);
        Assert.Equal(6.53, result.Items[0].Draft);
        Assert.Equal(result.Items.OrderBy(r => r.Km), result.Items);
    }

    [Fact]
    public async Task GetHydrostatics_SortParameters_AreCaseInsensitive()
    {
        PagedResult<HydrostaticRow> result = await GetPage($"{LargePontoon}?sortBy=Displacement&sortDir=DESC");

        Assert.Equal(12.00, result.Items[0].Draft);
    }

    // ---- Filtering ----

    [Fact]
    public async Task GetHydrostatics_DraftRange_FiltersBeforePaging()
    {
        PagedResult<HydrostaticRow> result = await GetPage($"{LargePontoon}?minDraft=3.00&maxDraft=3.49&pageSize=100");

        Assert.Equal(50, result.TotalCount);  // 3.00, 3.01, … 3.49
        Assert.Equal(1, result.TotalPages);
        Assert.All(result.Items, row => Assert.InRange(row.Draft, 3.00, 3.49));
    }

    // ---- 400 and 404 ----

    [Theory]
    [InlineData("page=0", "page")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=201", "pageSize")]
    [InlineData("sortBy=volume", "sortBy")]
    [InlineData("sortDir=up", "sortDir")]
    [InlineData("minDraft=-1", "minDraft")]
    [InlineData("minDraft=5&maxDraft=4", "minDraft")]
    public async Task GetHydrostatics_InvalidParameter_Returns400NamingTheParameter(string queryString, string invalidParameter)
    {
        HttpResponseMessage response = await _client.GetAsync($"{LargePontoon}?{queryString}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Contains(invalidParameter, problem!.Errors.Keys);
    }

    [Fact]
    public async Task GetHydrostatics_UnparsableNumber_Returns400ProblemDetails()
    {
        HttpResponseMessage response = await _client.GetAsync($"{LargePontoon}?page=abc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("/api/hulls/99/hydrostatics")]
    [InlineData("/api/hulls/99/hydrostatics/curve")]
    public async Task UnknownHull_Returns404ProblemDetails(string url)
    {
        HttpResponseMessage response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    // ---- Curve ----

    [Fact]
    public async Task GetCurve_Default_Returns100PointsAcrossFullDraftRange()
    {
        var curve = await _client.GetFromJsonAsync<List<CurvePoint>>($"{LargePontoon}/curve");

        Assert.Equal(100, curve!.Count);
        Assert.Equal(0.10, curve[0].Draft);
        Assert.Equal(12.0, curve[^1].Draft);
    }

    [Fact]
    public async Task GetCurve_PointsParameter_ControlsCount()
    {
        var curve = await _client.GetFromJsonAsync<List<CurvePoint>>($"{LargePontoon}/curve?points=5");

        Assert.Equal(5, curve!.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1001)]
    public async Task GetCurve_PointsOutOfRange_Returns400(int points)
    {
        HttpResponseMessage response = await _client.GetAsync($"{LargePontoon}/curve?points={points}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Page of draft ----

    [Fact]
    public async Task GetPageOf_ReturnsThePageContainingTheDraft()
    {
        // Ascending: 3.25 m is row index 315 → page 315 / 25 + 1 = 13.
        PagedResult<HydrostaticRow> result = await GetPage($"{LargePontoon}/page-of?draft=3.25&pageSize=25");

        Assert.Equal(13, result.Page);
        Assert.Contains(result.Items, row => row.Draft == 3.25);
    }

    [Fact]
    public async Task GetPageOf_RespectsSortOrder()
    {
        // Descending: 3.25 m is index 1190 − 315 = 875 → page 875 / 25 + 1 = 36.
        PagedResult<HydrostaticRow> result = await GetPage($"{LargePontoon}/page-of?draft=3.25&sortDir=desc");

        Assert.Equal(36, result.Page);
        Assert.Contains(result.Items, row => row.Draft == 3.25);
    }

    [Theory]
    [InlineData("3.2547", 3.25, 13)]  // snapped to the nearest centimetre
    [InlineData("0", 0.10, 1)]        // below the table: first row
    [InlineData("99", 12.00, 48)]     // beyond the depth: last row
    public async Task GetPageOf_SnapsToNearestTableRow(string draft, double expectedRowDraft, int expectedPage)
    {
        PagedResult<HydrostaticRow> result = await GetPage($"{LargePontoon}/page-of?draft={draft}");

        Assert.Equal(expectedPage, result.Page);
        Assert.Contains(result.Items, row => row.Draft == expectedRowDraft);
    }

    [Theory]
    [InlineData("", "draft")]
    [InlineData("draft=-1", "draft")]
    [InlineData("draft=NaN", "draft")]
    [InlineData("draft=3&pageSize=500", "pageSize")]
    [InlineData("draft=3&sortBy=volume", "sortBy")]
    public async Task GetPageOf_InvalidParameter_Returns400(string queryString, string invalidParameter)
    {
        HttpResponseMessage response = await _client.GetAsync($"{LargePontoon}/page-of?{queryString}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Contains(invalidParameter, problem!.Errors.Keys);
    }

    [Fact]
    public async Task GetPageOf_UnknownHull_Returns404()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/hulls/99/hydrostatics/page-of?draft=1");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Swagger ----

    [Fact]
    public async Task Swagger_DocumentListsAllEndpoints()
    {
        string json = await _client.GetStringAsync("/swagger/v1/swagger.json");

        Assert.Contains("/api/hulls/{id}/hydrostatics", json);
        Assert.Contains("/api/hulls/{id}/hydrostatics/curve", json);
    }

    private async Task<PagedResult<HydrostaticRow>> GetPage(string url)
    {
        HttpResponseMessage response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PagedResult<HydrostaticRow>>())!;
    }
}
