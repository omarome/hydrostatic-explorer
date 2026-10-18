using System.Globalization;
using System.Net;
using System.Text;
using HydroExplorer.App.Services;
using HydroExplorer.App.ViewModels;
using HydroExplorer.Core.Models;

namespace HydroExplorer.App.Tests;

/// <summary>
/// Tests the real HttpClient-based client against a stub HTTP handler (no network):
/// URL building, error mapping, and status reporting for the status bar.
/// </summary>
public class HydroApiClientTests
{
    private readonly StubHandler _handler = new();
    private readonly ApiStatus _status = new();
    private readonly HttpClient _http;
    private readonly HydroApiClient _client;

    public HydroApiClientTests()
    {
        _http = new HttpClient(_handler) { BaseAddress = new Uri("http://test/") };
        _client = new HydroApiClient(_http, _status);
    }

    [Fact]
    public async Task PageOfDraftUrl_UsesADecimalPoint_EvenWithACommaLocale()
    {
        // Swedish formatting writes 3.25 as "3,25", which the API would reject.
        CultureInfo original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("sv-SE");
        try
        {
            _handler.RespondWithJson("""{"items":[],"page":13,"pageSize":25,"totalCount":1191,"totalPages":48}""");

            await _client.GetPageContainingDraftAsync(3, 3.25, new HydrostaticsPageRequest(1, 25, "draft", SortDescending: true));

            Assert.Equal(
                "http://test/api/hulls/3/hydrostatics/page-of?draft=3.25&pageSize=25&sortBy=draft&sortDir=desc",
                _handler.LastRequestUri?.ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task Success_ParsesJson_AndReportsConnected()
    {
        _handler.RespondWithJson("""[{"id":1,"name":"Small Barge","length":40,"beam":10,"depth":4}]""");

        IReadOnlyList<Hull> hulls = await _client.GetHullsAsync();

        Assert.Equal(new Hull(1, "Small Barge", 40, 10, 4), Assert.Single(hulls));
        Assert.Equal(ApiConnectionState.Connected, _status.State);
        Assert.NotNull(_status.LastSuccessTime);
    }

    [Fact]
    public async Task ConnectionRefused_ThrowsFriendlyError_AndReportsDisconnected()
    {
        _handler.Respond = _ => throw new HttpRequestException("Connection refused");

        var ex = await Assert.ThrowsAsync<HydroApiException>(() => _client.GetHullsAsync());

        Assert.Contains("Cannot reach the API at http://test/", ex.Message);
        Assert.Equal(ApiConnectionState.Disconnected, _status.State);
        Assert.Equal(ex.Message, _status.LastError);
    }

    [Fact]
    public async Task ErrorStatusCode_ThrowsWithTheStatus()
    {
        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);

        var ex = await Assert.ThrowsAsync<HydroApiException>(() => _client.GetHullsAsync());

        Assert.Contains("500", ex.Message);
    }

    [Fact]
    public async Task StatusBar_ShowsConnectionAndLastLoad()
    {
        var statusBar = new StatusBarViewModel(_status, _http);
        Assert.Equal("Connecting to http://test/…", statusBar.ConnectionText);
        Assert.Equal("No data loaded yet", statusBar.LastLoadText);

        _handler.RespondWithJson("[]");
        await _client.GetHullsAsync();

        Assert.True(statusBar.IsConnected);
        Assert.Equal("Connected to http://test/", statusBar.ConnectionText);
        Assert.StartsWith("Last load ", statusBar.LastLoadText);

        _handler.Respond = _ => throw new HttpRequestException("down");
        await Assert.ThrowsAsync<HydroApiException>(() => _client.GetHullsAsync());

        Assert.True(statusBar.IsDisconnected);
        Assert.StartsWith("API unavailable: Cannot reach", statusBar.ConnectionText);
    }

    /// <summary>
    /// An HttpMessageHandler is the pluggable "transport" under HttpClient. Replacing it lets tests
    /// answer requests in memory, a bit like mocking fetch in a JavaScript test.
    /// </summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.NotFound);

        public Uri? LastRequestUri { get; private set; }

        public void RespondWithJson(string json) =>
            Respond = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(Respond(request));
        }
    }
}
