using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HydroExplorer.App.Services;
using HydroExplorer.Core.Models;
using HydroExplorer.Core.Paging;

namespace HydroExplorer.App.ViewModels;

/// <summary>
/// State and commands for the hull selector and the paginated hydrostatic table.
/// </summary>
/// <remarks>
/// How CommunityToolkit.Mvvm source generators work (they write code at compile time):
/// <list type="bullet">
/// <item>[ObservableProperty] on field <c>_currentPage</c> generates a public <c>CurrentPage</c> property
///   that raises PropertyChanged, so bound controls update (like setState triggering a re-render).</item>
/// <item>[RelayCommand] on method <c>NextPageAsync</c> generates <c>NextPageCommand</c>, an ICommand that
///   buttons bind to. For async methods the command also tracks "is running".</item>
/// <item>[NotifyCanExecuteChangedFor] re-evaluates a command's CanExecute when the property changes,
///   which enables or disables the bound button.</item>
/// <item>A partial method <c>OnSelectedHullChanged</c> is called after <c>SelectedHull</c> changes
///   (similar to useEffect with [selectedHull] as the dependency).</item>
/// </list>
/// </remarks>
public partial class HydroTableViewModel : ViewModelBase
{
    public static IReadOnlyList<int> PageSizeOptions { get; } = new[] { 25, 50, 100 };

    /// <summary>Columns the API can sort by (values of <see cref="SortBy"/>).</summary>
    public static IReadOnlyList<string> SortFields { get; } = new[] { "draft", "displacement", "km" };

    private readonly IHydroApiClient _api;

    // Cancels the in-flight page request when a newer one starts (e.g. the user switches hull mid-load).
    private CancellationTokenSource? _pageLoadCancellation;

    public HydroTableViewModel(IHydroApiClient api)
    {
        _api = api;
    }

    [ObservableProperty]
    private IReadOnlyList<Hull> _hulls = Array.Empty<Hull>();

    [ObservableProperty]
    private Hull? _selectedHull;

    [ObservableProperty]
    private IReadOnlyList<HydrostaticRow> _rows = Array.Empty<HydrostaticRow>();

    [ObservableProperty]
    private HydrostaticRow? _selectedRow;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    [NotifyCanExecuteChangedFor(nameof(FirstPageCommand), nameof(PreviousPageCommand),
                                nameof(NextPageCommand), nameof(LastPageCommand))]
    private int _currentPage = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand), nameof(LastPageCommand))]
    private int _totalPages;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    private int _totalCount;

    [ObservableProperty]
    private int _pageSize = PageSizeOptions[0];

    [ObservableProperty]
    private string _sortBy = "draft";

    [ObservableProperty]
    private bool _sortDescending;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FirstPageCommand), nameof(PreviousPageCommand),
                                nameof(NextPageCommand), nameof(LastPageCommand), nameof(RefreshCommand))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => ErrorMessage is not null;

    /// <summary>For example "Page 3 of 48 (1,191 rows)".</summary>
    public string PageSummary => TotalPages == 0
        ? "No rows"
        : $"Page {CurrentPage} of {TotalPages} ({TotalCount:N0} rows)";

    // ---- Loading ----

    /// <summary>Loads the hull list, then selects the first hull (which loads its first page).</summary>
    public async Task LoadHullsAsync()
    {
        IReadOnlyList<Hull> hulls;
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            hulls = await _api.GetHullsAsync();
        }
        catch (HydroApiException ex)
        {
            ErrorMessage = ex.Message;
            return;
        }
        finally
        {
            IsLoading = false;
        }

        Hulls = hulls;
        SelectedHull = hulls.FirstOrDefault();  // triggers OnSelectedHullChanged → loads page 1
    }

    /// <summary>
    /// Loads <paramref name="page"/> for the selected hull. Page state (rows, page number, totals)
    /// only changes when the request succeeds, so a failure leaves the previous page visible.
    /// Never throws: API errors go to <see cref="ErrorMessage"/>.
    /// </summary>
    public async Task LoadPageAsync(int page)
    {
        if (SelectedHull is not { } hull)
        {
            return;
        }

        _pageLoadCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _pageLoadCancellation = cancellation;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var request = new HydrostaticsPageRequest(page, PageSize, SortBy, SortDescending);
            PagedResult<HydrostaticRow> result = await _api.GetHydrostaticsAsync(hull.Id, request, cancellation.Token);

            // "await" resumes here later. If a newer request started meanwhile, this result is stale.
            if (cancellation.IsCancellationRequested)
            {
                return;
            }

            Rows = result.Items;
            CurrentPage = result.Page;
            TotalPages = result.TotalPages;
            TotalCount = result.TotalCount;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Superseded by a newer request. Nothing to do.
        }
        catch (HydroApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            // Only the latest request is allowed to end the loading state.
            if (_pageLoadCancellation == cancellation)
            {
                _pageLoadCancellation = null;
                IsLoading = false;
            }

            cancellation.Dispose();
        }
    }

    // Generated partial-method hooks. "_ = SomeAsync()" starts the load without awaiting it
    // ("fire and forget"). That's safe here because LoadPageAsync catches its own errors.

    partial void OnSelectedHullChanged(Hull? value) => _ = LoadPageAsync(1);

    partial void OnPageSizeChanged(int value) => _ = LoadPageAsync(1);

    // ---- Commands ----

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private Task FirstPageAsync() => LoadPageAsync(1);

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private Task PreviousPageAsync() => LoadPageAsync(CurrentPage - 1);

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private Task NextPageAsync() => LoadPageAsync(CurrentPage + 1);

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private Task LastPageAsync() => LoadPageAsync(TotalPages);

    /// <summary>Reloads the current page, or the hull list if it never loaded. Also used by the error banner's Retry button.</summary>
    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private Task RefreshAsync() => Hulls.Count == 0 ? LoadHullsAsync() : LoadPageAsync(CurrentPage);

    /// <summary>
    /// Sorts by <paramref name="field"/> on the server. Clicking the current sort column again flips
    /// the direction. Sorting always returns to page 1.
    /// </summary>
    [RelayCommand]
    private Task SortByAsync(string field)
    {
        if (!SortFields.Contains(field))
        {
            throw new ArgumentException($"Cannot sort by '{field}'.", nameof(field));
        }

        if (field == SortBy)
        {
            SortDescending = !SortDescending;
        }
        else
        {
            SortBy = field;
            SortDescending = false;
        }

        return LoadPageAsync(1);
    }

    private bool CanGoBack() => !IsLoading && CurrentPage > 1;

    private bool CanGoForward() => !IsLoading && CurrentPage < TotalPages;

    private bool CanRefresh() => !IsLoading;
}
