using System.ComponentModel;
using Avalonia.Controls;
using HydroExplorer.App.ViewModels;

namespace HydroExplorer.App.Views;

/// <summary>
/// Code-behind for the table. It holds only view glue that XAML bindings can't express:
/// sending header clicks to the ViewModel and drawing the sort arrow in the header.
/// </summary>
public partial class HydroTableView : UserControl
{
    private HydroTableViewModel? _viewModel;

    public HydroTableView()
    {
        InitializeComponent();
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
