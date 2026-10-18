using System.ComponentModel;
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
    private readonly SelectionService _selection;

    // Cancels the in-flight page request when a newer one starts (e.g. the user switches hull mid-load).
    private CancellationTokenSource? _pageLoadCancellation;

    public HydroTableViewModel(IHydroApiClient api, SelectionService selection)
    {
        _api = api;
        _selection = selection;
        _selection.PropertyChanged += OnSelectionChanged;  // app-lifetime singletons: no unsubscribe needed
    }

    [ObservableProperty]
    private IReadOnlyList<Hull> _hulls = Array.Empty<Hull>();

    [ObservableProperty]
    private Hull? _selectedHull;

    [ObservableProperty]
    private IReadOnlyList<HydrostaticRow> _rows = Array.Empty<HydrostaticRow>();

    /// <summary>The highlighted row: the shared selected draft, if it is on the current page.</summary>
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

    /// <summary>Loads page <paramref name="page"/> for the selected hull. Never throws.</summary>
    public Task LoadPageAsync(int page) =>
        LoadAsync((hullId, cancellationToken) =>
            _api.GetHydrostaticsAsync(hullId, CurrentRequest(page), cancellationToken));

    /// <summary>Loads whichever page contains <paramref name="draft"/> (in the current sort order). Never throws.</summary>
    public Task LoadPageContainingDraftAsync(double draft) =>
        LoadAsync((hullId, cancellationToken) =>
            _api.GetPageContainingDraftAsync(hullId, draft, CurrentRequest(page: 1), cancellationToken));

    private HydrostaticsPageRequest CurrentRequest(int page) => new(page, PageSize, SortBy, SortDescending);

    /// <summary>
    /// Runs one page request and applies the result. <paramref name="fetch"/> is the request to make,
    /// passed in as a function (like passing a callback in JavaScript), so both kinds of page load share
    /// the loading, cancellation and error handling below.
    /// </summary>
    /// <remarks>
    /// Page state (rows, page number, totals) only changes when the request succeeds, so a failure leaves
    /// the previous page visible. API errors go to <see cref="ErrorMessage"/>; this method never throws.
    /// </remarks>
    private async Task LoadAsync(Func<int, CancellationToken, Task<PagedResult<HydrostaticRow>>> fetch)
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
            PagedResult<HydrostaticRow> result = await fetch(hull.Id, cancellation.Token);

            // "await" resumes here later. If a newer request started meanwhile, this result is stale.
            if (cancellation.IsCancellationRequested)
            {
                return;
            }

            Rows = result.Items;
            CurrentPage = result.Page;
            TotalPages = result.TotalPages;
            TotalCount = result.TotalCount;

            // Highlight the shared selection if it is on this page (e.g. after paging away and back).
            SelectedRow = FindRow(_selection.SelectedDraft);
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

    // ---- Linking with the shared selection ----

    // Generated partial-method hooks. "_ = SomeAsync()" starts the load without awaiting it
    // ("fire and forget"). That's safe here because the load methods catch their own errors.

    partial void OnSelectedHullChanged(Hull? value)
    {
        _selection.SelectedHull = value;  // also clears the shared draft
        _ = LoadPageAsync(1);
    }

    /// <summary>
    /// Publishes the row the user clicked to the shared selection. A null row is ignored: the DataGrid
    /// clears its selection whenever the page changes, but the selected draft should stay selected
    /// (and stay marked in the chart and hull view) while the user pages through the table.
    /// </summary>
    partial void OnSelectedRowChanged(HydrostaticRow? value)
    {
        if (value is not null)
        {
            _selection.SelectedDraft = value.Draft;
        }
    }

    partial void OnPageSizeChanged(int value) => _ = LoadPageAsync(1);

    private void OnSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectionService.SelectedDraft))
        {
            ShowSelectedDraft();
        }
    }

    /// <summary>
    /// Makes the table show the shared draft: highlight it if it is on this page, otherwise
    /// fetch the page that contains it (the chart or slider may pick a draft on any page).
    /// </summary>
    private void ShowSelectedDraft()
    {
        double? draft = _selection.SelectedDraft;
        HydrostaticRow? row = FindRow(draft);
        if (draft is null || row is not null)
        {
            // The draft is on this page (or cleared). Any page still loading for an older draft
            // would now show the wrong page, so cancel it.
            _pageLoadCancellation?.Cancel();
            SelectedRow = row;
            return;
        }

        _ = LoadPageContainingDraftAsync(draft.Value);
    }

    /// <summary>
    /// The row on the current page with exactly this draft. Exact equality is safe because every
    /// draft is snapped to the table's centimetre grid (see SnapToTableDraft).
    /// </summary>
    private HydrostaticRow? FindRow(double? draft) =>
        draft is null ? null : Rows.FirstOrDefault(row => row.Draft == draft);

    // ---- Keyboard ----

    /// <summary>
    /// Moves the selection one row down (<paramref name="step"/> = 1) or up (−1). At the edge of the page
    /// it continues on the next or previous page, so the arrow keys walk through the whole table.
    /// </summary>
    public async Task MoveSelectionAsync(int step)
    {
        if (Rows.Count == 0 || IsLoading)
        {
            return;
        }

        int index = SelectedRow is null ? -1 : Rows.ToList().IndexOf(SelectedRow);
        if (index < 0)
        {
            // Nothing selected on this page yet: start at the top (down) or bottom (up).
            SelectedRow = step > 0 ? Rows[0] : Rows[^1];
            return;
        }

        int target = index + step;
        if (target >= 0 && target < Rows.Count)
        {
            SelectedRow = Rows[target];
            return;
        }

        int page = CurrentPage + Math.Sign(step);
        if (page < 1 || page > TotalPages)
        {
            return;  // already at the first or last row of the whole table
        }

        await LoadPageAsync(page);
        if (CurrentPage == page && Rows.Count > 0)
        {
            SelectedRow = step > 0 ? Rows[0] : Rows[^1];
        }
    }

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
