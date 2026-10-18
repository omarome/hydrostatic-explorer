using System.ComponentModel;
using System.Windows.Input;  // ICommand lives here in all of .NET (a WPF-era name), and Avalonia uses it too
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using HydroExplorer.App.ViewModels;

namespace HydroExplorer.App.Views;

/// <summary>
/// Code-behind for the table. It holds only view glue that XAML bindings can't express:
/// sending header clicks and keys to the ViewModel, drawing the sort arrow in the header,
/// and scrolling the selected row into view.
/// </summary>
public partial class HydroTableView : UserControl
{
    private HydroTableViewModel? _viewModel;

    public HydroTableView()
    {
        InitializeComponent();

        // "Tunnel" routing: this handler runs on the way *down* to the focused DataGrid, before the grid's
        // own key handling, so we decide what arrows and PageUp/PageDown do (including crossing pages).
        // WPF note: WPF expresses the same thing with separate "Preview" events (PreviewKeyDown).
        AddHandler(KeyDownEvent, OnTableKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>Up/Down move the selection (continuing onto the next/previous page); PageUp/PageDown change page.</summary>
    private void OnTableKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
                _ = _viewModel.MoveSelectionAsync(+1);
                e.Handled = true;
                break;
            case Key.Up:
                _ = _viewModel.MoveSelectionAsync(-1);
                e.Handled = true;
                break;
            case Key.PageDown:
                ExecuteIfEnabled(_viewModel.NextPageCommand);
                e.Handled = true;
                break;
            case Key.PageUp:
                ExecuteIfEnabled(_viewModel.PreviousPageCommand);
                e.Handled = true;
                break;
        }
    }

    private static void ExecuteIfEnabled(ICommand command)
    {
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    /// <summary>
    /// Called when a sortable column header is clicked. Without handling, the DataGrid would sort
    /// only the 25 rows it has; instead we cancel that and ask the server to sort the whole table.
    /// </summary>
    private void OnGridSorting(object? sender, DataGridColumnEventArgs e)
    {
        e.Handled = true;
        if (_viewModel is not null && e.Column.SortMemberPath is { } field)
        {
            _viewModel.SortByCommand.Execute(field);
        }
    }

    // DataContext is set from outside (MainWindow binds it to MainViewModel.Table), so we watch for it here.
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as HydroTableViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdateSortArrows();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HydroTableViewModel.SortBy) or nameof(HydroTableViewModel.SortDescending))
        {
            UpdateSortArrows();
        }
        else if (e.PropertyName == nameof(HydroTableViewModel.SelectedRow) && _viewModel?.SelectedRow is { } row)
        {
            // A selection made elsewhere (chart, slider, keys) may be off-screen on a 100-row page.
            TableGrid.ScrollIntoView(row, null);
        }
    }

    /// <summary>
    /// Adds ▲/▼ to the sorted column's header. DataGrid columns aren't in the visual tree, so binding
    /// their Header to the ViewModel is awkward; setting it here is simpler.
    /// </summary>
    private void UpdateSortArrows()
    {
        if (_viewModel is null)
        {
            return;
        }

        foreach (DataGridColumn column in TableGrid.Columns)
        {
            string header = (column.Header as string ?? string.Empty).TrimEnd(' ', '▲', '▼');
            bool isSorted = column.SortMemberPath == _viewModel.SortBy;
            column.Header = isSorted ? $"{header} {(_viewModel.SortDescending ? "▼" : "▲")}" : header;
        }
    }
}
