using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using Clipt.Core;
using Clipt.Core.Models;
using Clipt.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Clipt.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private const int SearchDebounceMs = 250;

    private readonly IHistoryService _historyService;
    private readonly ISearchService _searchService;
    private readonly IClipboardMonitor _clipboardMonitor;
    private readonly ILogger<MainViewModel> _logger;
    private CancellationTokenSource? _searchCts;

    private bool _isDemoFallback;
    private List<ClipboardItem> _demoItems = [];

    public MainViewModel(
        IHistoryService historyService,
        ISearchService searchService,
        IClipboardMonitor clipboardMonitor,
        ILogger<MainViewModel> logger)
    {
        _historyService = historyService;
        _searchService = searchService;
        _clipboardMonitor = clipboardMonitor;
        _logger = logger;
        _clipboardMonitor.ClipboardItemCaptured += OnClipboardItemCaptured;
        ItemsView = CollectionViewSource.GetDefaultView(Items);
        ItemsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ClipboardItemViewModel.GroupName)));
        ItemsView.SortDescriptions.Add(new SortDescription(nameof(ClipboardItemViewModel.IsPinned), ListSortDirection.Descending));
        ItemsView.SortDescriptions.Add(new SortDescription(nameof(ClipboardItemViewModel.CreatedAt), ListSortDirection.Descending));
    }

    public ObservableCollection<ClipboardItemViewModel> Items { get; } = [];

    public ICollectionView ItemsView { get; }

    [ObservableProperty]
    public partial ClipboardItemViewModel? SelectedItem { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsWorkMode { get; set; }

    [ObservableProperty]
    public partial string ActiveNavigationItem { get; set; } = NavigationItems.Clipboard;

    public bool IsClipboardView => ActiveNavigationItem == NavigationItems.Clipboard;

    public string ModeLabel => IsWorkMode ? "Work" : "Capture";

    public double TargetWidth => IsWorkMode ? 880 : 380;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (Items.Count > 0)
        {
            return;
        }

        var items = await _historyService.GetItemsAsync(cancellationToken);

        if (items.Count == 0)
        {
            _logger.LogInformation("Database is empty, using demo data for showroom experience.");
            var demoItems = DesignTimeData.GetSampleItems();
            _demoItems = [.. demoItems];
            _isDemoFallback = true;
            PopulateItems(demoItems);
        }
        else
        {
            PopulateItems(items);
        }

        SelectedItem = Items.FirstOrDefault();
    }

    [RelayCommand]
    private void ToggleMode()
    {
        IsWorkMode = !IsWorkMode;
    }

    [RelayCommand]
    private void Navigate(string item)
    {
        ActiveNavigationItem = item;
        OnPropertyChanged(nameof(IsClipboardView));
    }

    [RelayCommand]
    private async Task TogglePin(ClipboardItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (_isDemoFallback)
        {
            var idx = _demoItems.FindIndex(i => i.Id == item.Id);
            if (idx >= 0)
            {
                _demoItems[idx] = _demoItems[idx] with { IsPinned = !_demoItems[idx].IsPinned };
            }

            var filtered = _searchService.Filter(_demoItems, SearchText);
            PopulateItems(filtered);
            SelectedItem = Items.FirstOrDefault(i => i.Id == item.Id);
            return;
        }

        try
        {
            var newPinnedState = !item.IsPinned;
            await _historyService.SetPinnedAsync(item.Id, newPinnedState, CancellationToken.None);
            await RefreshItemsAsync();
            SelectedItem = Items.FirstOrDefault(i => i.Id == item.Id);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to toggle pin for item {Id}.", item.Id);
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        DebounceSearch(value);
    }

    partial void OnIsWorkModeChanged(bool value)
    {
        OnPropertyChanged(nameof(ModeLabel));
        OnPropertyChanged(nameof(TargetWidth));
    }

    private async void DebounceSearch(string query)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try
        {
            await Task.Delay(SearchDebounceMs, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested)
        {
            return;
        }

        if (_isDemoFallback)
        {
            var results = _searchService.Filter(_demoItems, query);
            PopulateItems(results);
            return;
        }

        try
        {
            var results = await _historyService.SearchAsync(query, token);
            PopulateItems(results);
        }
        catch (OperationCanceledException)
        {
            // Search was superseded by a newer query.
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Search failed for query '{Query}'.", query);
        }
    }

    private async Task RefreshItemsAsync()
    {
        if (_isDemoFallback)
        {
            var filtered = _searchService.Filter(_demoItems, SearchText);
            PopulateItems(filtered);
            return;
        }

        try
        {
            var results = await _historyService.SearchAsync(SearchText, CancellationToken.None);
            PopulateItems(results);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to refresh items.");
        }
    }

    private void PopulateItems(IReadOnlyList<Clipt.Core.Models.ClipboardItem> results)
    {
        Items.Clear();
        foreach (var model in results)
        {
            Items.Add(new ClipboardItemViewModel(model));
        }
    }

    private async void OnClipboardItemCaptured(object? sender, Clipt.Core.Models.ClipboardItem item)
    {
        var savedItem = item;
        try
        {
            savedItem = await _historyService.SaveAsync(item, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to persist captured clipboard item {Id}.", item.Id);
        }

        if (_isDemoFallback)
        {
            _logger.LogInformation("Real clipboard item captured, exiting demo fallback mode.");
            _isDemoFallback = false;
            _demoItems.Clear();
            Items.Clear();
        }

        var existing = Items.FirstOrDefault(candidate => candidate.Id == savedItem.Id);
        if (existing is not null)
        {
            Items.Remove(existing);
        }

        var viewModel = new ClipboardItemViewModel(savedItem);
        Items.Insert(0, viewModel);
        SelectedItem = viewModel;
    }
}
