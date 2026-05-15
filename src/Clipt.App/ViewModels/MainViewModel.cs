using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using Clipt.App.Services;
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
    private const int SearchHistoryLimit = 8;

    private readonly IHistoryService _historyService;
    private readonly ISearchService _searchService;
    private readonly IClipboardMonitor _clipboardMonitor;
    private readonly ISettingsService _settingsService;
    private readonly ImagePreviewCache _imagePreviewCache;
    private readonly ILogger<MainViewModel> _logger;
    private CancellationTokenSource? _searchCts;

    private bool _isDemoFallback;
    private List<ClipboardItem> _demoItems = [];

    private int _cachedMaxHistoryItems = 500;
    private int _cachedAutoPruneAfterDays;

    /// <summary>
    /// Updates the in-memory history cap after settings are saved (no restart).
    /// </summary>
    public void SetCachedMaxHistoryItems(int maxHistoryItems)
    {
        _cachedMaxHistoryItems = Math.Max(0, maxHistoryItems);
    }

    /// <summary>
    /// Updates the in-memory age-based prune threshold after settings are saved.
    /// Zero or negative values disable age-based pruning.
    /// </summary>
    public void SetCachedAutoPruneAfterDays(int autoPruneAfterDays)
    {
        _cachedAutoPruneAfterDays = Math.Max(0, autoPruneAfterDays);
    }

    public MainViewModel(
        IHistoryService historyService,
        ISearchService searchService,
        IClipboardMonitor clipboardMonitor,
        ISettingsService settingsService,
        ImagePreviewCache imagePreviewCache,
        ILogger<MainViewModel> logger)
    {
        _historyService = historyService;
        _searchService = searchService;
        _clipboardMonitor = clipboardMonitor;
        _settingsService = settingsService;
        _imagePreviewCache = imagePreviewCache;
        _logger = logger;
        _clipboardMonitor.ClipboardItemCaptured += OnClipboardItemCaptured;
        ItemsView = CollectionViewSource.GetDefaultView(Items);
        ItemsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ClipboardItemViewModel.GroupName)));
        ItemsView.SortDescriptions.Add(new SortDescription(nameof(ClipboardItemViewModel.IsPinned), ListSortDirection.Descending));
        ItemsView.SortDescriptions.Add(new SortDescription(nameof(ClipboardItemViewModel.CreatedAt), ListSortDirection.Descending));
    }

    public ObservableCollection<ClipboardItemViewModel> Items { get; } = [];

    public ICollectionView ItemsView { get; }

    public ObservableCollection<string> SearchHistory { get; } = [];

    public IReadOnlyList<ContentTypeFilterViewModel> ContentTypeFilters { get; } = BuildContentTypeFilters();

    [ObservableProperty]
    public partial ClipboardItemViewModel? SelectedItem { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ContentType? ActiveContentTypeFilter { get; set; }

    [ObservableProperty]
    public partial bool IsPinnedFilterActive { get; set; }

    [ObservableProperty]
    public partial bool IsWorkMode { get; set; }

    [ObservableProperty]
    public partial string ActiveNavigationItem { get; set; } = NavigationItems.Clipboard;

    [ObservableProperty]
    public partial bool IsCapturePaused { get; set; }

    public bool IsClipboardView => ActiveNavigationItem == NavigationItems.Clipboard;

    public string ModeLabel => IsWorkMode ? "Work" : "Capture";

    public double TargetWidth => IsWorkMode ? 880 : 380;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (Items.Count > 0)
        {
            return;
        }

        // Cache history limit from settings once at startup.
        try
        {
            var settings = await _settingsService.GetAsync(cancellationToken);
            _cachedMaxHistoryItems = settings.MaxHistoryItems;
            _cachedAutoPruneAfterDays = Math.Max(0, settings.AutoPruneAfterDays ?? 0);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to load settings for history limit; using default {Default}.", _cachedMaxHistoryItems);
        }

        // Prune aged items before loading the initial view so we don't briefly
        // surface rows that are about to disappear.
        try
        {
            var ageResult = await _historyService.PruneOlderThanAsync(_cachedAutoPruneAfterDays, cancellationToken);
            if (ageResult.Count > 0)
            {
                _imagePreviewCache.TryDeletePreviews(ageResult.ImageUris);
                _logger.LogInformation("Pruned {Count} aged clipboard items at startup.", ageResult.Count);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Startup age-based prune failed; continuing with current items.");
        }

        var items = await _historyService.GetItemsAsync(cancellationToken);

        if (items.Count == 0)
        {
            _logger.LogInformation("Database is empty, using demo data for showroom experience.");
            var demoItems = DesignTimeData.GetSampleItems();
            _demoItems = [.. demoItems];
            _isDemoFallback = true;
            UpdateContentTypeFilterCounts(demoItems);
            PopulateItems(demoItems);
        }
        else
        {
            UpdateContentTypeFilterCounts(items);
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
    private async Task DeleteItem(ClipboardItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            if (_isDemoFallback)
            {
                _demoItems.RemoveAll(i => i.Id == item.Id);
                _clipboardMonitor.ResetDuplicateTracking();
                await RefreshItemsAsync();
                SelectedItem = GetNextSelectionAfterDelete();
                return;
            }

            await _historyService.DeleteAsync(item.Id, CancellationToken.None);
            _imagePreviewCache.TryDeletePreview(item.ImageUri);
            _clipboardMonitor.ResetDuplicateTracking();
            await RefreshItemsAsync();
            SelectedItem = GetNextSelectionAfterDelete();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to delete item {Id}.", item.Id);
        }
    }

    [RelayCommand]
    private async Task ClearUnpinned()
    {
        try
        {
            if (_isDemoFallback)
            {
                _demoItems.RemoveAll(i => !i.IsPinned);
                _clipboardMonitor.ResetDuplicateTracking();
                await RefreshItemsAsync();
                SelectedItem = Items.FirstOrDefault();
                return;
            }

            var clearResult = await _historyService.ClearUnpinnedAsync(CancellationToken.None);
            _imagePreviewCache.TryDeletePreviews(clearResult.ImageUris);
            _clipboardMonitor.ResetDuplicateTracking();
            await RefreshItemsAsync();
            SelectedItem = Items.FirstOrDefault();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to clear unpinned items.");
        }
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

            var baseItems = _searchService.Filter(_demoItems, SearchText);
            UpdateContentTypeFilterCounts(baseItems);
            PopulateItems(ApplyActiveFilter(baseItems));
            SelectedItem = Items.FirstOrDefault(i => i.Id == item.Id) ?? Items.FirstOrDefault();
            return;
        }

        try
        {
            var newPinnedState = !item.IsPinned;
            await _historyService.SetPinnedAsync(item.Id, newPinnedState, CancellationToken.None);
            await RefreshItemsAsync();
            SelectedItem = Items.FirstOrDefault(i => i.Id == item.Id) ?? Items.FirstOrDefault();
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
            var baseItems = _searchService.Filter(_demoItems, query);
            UpdateContentTypeFilterCounts(baseItems);
            PopulateItems(ApplyActiveFilter(baseItems));
            RememberSearchQuery(query, Items.Count);
            return;
        }

        try
        {
            var baseItems = await _historyService.SearchAsync(query, token);
            UpdateContentTypeFilterCounts(baseItems);
            PopulateItems(ApplyActiveFilter(baseItems));
            RememberSearchQuery(query, Items.Count);
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

    /// <summary>
    /// Exits demo fallback mode and reloads the item list from the database.
    /// Call this after demo content has been seeded into the database so the
    /// main window shows real persisted rows instead of in-memory samples.
    /// </summary>
    public async Task RefreshFromDatabaseAsync()
    {
        _isDemoFallback = false;
        _demoItems.Clear();
        await RefreshItemsAsync();
        SelectedItem = Items.FirstOrDefault();
    }

    private async Task RefreshItemsAsync()
    {
        if (_isDemoFallback)
        {
            var baseItems = _searchService.Filter(_demoItems, SearchText);
            UpdateContentTypeFilterCounts(baseItems);
            PopulateItems(ApplyActiveFilter(baseItems));
            return;
        }

        try
        {
            var baseItems = await _historyService.SearchAsync(SearchText, CancellationToken.None);
            UpdateContentTypeFilterCounts(baseItems);
            PopulateItems(ApplyActiveFilter(baseItems));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to refresh items.");
        }
    }

    private void UpdateContentTypeFilterCounts(IReadOnlyList<ClipboardItem> baseItems)
    {
        foreach (var filter in ContentTypeFilters)
        {
            filter.Count = filter.ContentType is null
                ? filter.IsPinnedFilter
                    ? baseItems.Count(i => i.IsPinned)
                    : baseItems.Count
                : baseItems.Count(i => i.ContentType == filter.ContentType.Value);
        }
    }

    [RelayCommand]
    private async Task SelectContentTypeFilter(ContentTypeFilterViewModel? filter)
    {
        if (filter is null)
        {
            return;
        }

        foreach (var f in ContentTypeFilters)
        {
            f.IsSelected = f == filter;
        }

        ActiveContentTypeFilter = filter.ContentType;
        IsPinnedFilterActive = filter.IsPinnedFilter;
        await RefreshItemsAsync();
    }

    [RelayCommand]
    private void SelectSearchHistory(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        SearchText = query;
    }

    private static IReadOnlyList<ContentTypeFilterViewModel> BuildContentTypeFilters()
    {
        return
        [
            new ContentTypeFilterViewModel("All", null) { IsSelected = true },
            new ContentTypeFilterViewModel("Text", ContentType.Text),
            new ContentTypeFilterViewModel("Code", ContentType.Code),
            new ContentTypeFilterViewModel("Markdown", ContentType.Markdown),
            new ContentTypeFilterViewModel("JSON", ContentType.Json),
            new ContentTypeFilterViewModel("URL", ContentType.Url),
            new ContentTypeFilterViewModel("Files", ContentType.File),
            new ContentTypeFilterViewModel("Images", ContentType.Image),
            new ContentTypeFilterViewModel("Color", ContentType.Color),
            new ContentTypeFilterViewModel("Pinned", null, isPinnedFilter: true),
        ];
    }

    private IReadOnlyList<ClipboardItem> ApplyActiveFilter(IReadOnlyList<ClipboardItem> items)
    {
        if (IsPinnedFilterActive)
        {
            return items.Where(i => i.IsPinned).ToList();
        }

        if (ActiveContentTypeFilter is null)
        {
            return items;
        }

        var type = ActiveContentTypeFilter.Value;
        return items.Where(i => i.ContentType == type).ToList();
    }

    private ClipboardItemViewModel? GetNextSelectionAfterDelete()
    {
        return Items.FirstOrDefault();
    }

    private void PopulateItems(IReadOnlyList<Clipt.Core.Models.ClipboardItem> results)
    {
        Items.Clear();
        foreach (var model in results)
        {
            Items.Add(new ClipboardItemViewModel(model));
        }
    }

    private void RememberSearchQuery(string query, int resultCount)
    {
        var trimmed = query.Trim();
        if (trimmed.Length < 2 || resultCount == 0)
        {
            return;
        }

        var existing = SearchHistory.FirstOrDefault(entry =>
            string.Equals(entry, trimmed, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            SearchHistory.Remove(existing);
        }

        SearchHistory.Insert(0, trimmed);
        while (SearchHistory.Count > SearchHistoryLimit)
        {
            SearchHistory.RemoveAt(SearchHistory.Count - 1);
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

        // Prune oldest unpinned items to stay within the configured limit, and
        // additionally drop any items that have aged past the configured cutoff.
        // Pinned items are never affected by pruning.
        // Pruning runs after every save (including duplicates) to ensure the
        // history stays bounded even if previous prunes were skipped.
        try
        {
            var maxItemsResult = await _historyService.PruneUnpinnedAsync(_cachedMaxHistoryItems, CancellationToken.None);
            var ageResult = await _historyService.PruneOlderThanAsync(_cachedAutoPruneAfterDays, CancellationToken.None);
            var totalRemoved = maxItemsResult.Count + ageResult.Count;
            if (totalRemoved > 0)
            {
                var prunedImageUris = maxItemsResult.ImageUris.Concat(ageResult.ImageUris).ToList();
                _imagePreviewCache.TryDeletePreviews(prunedImageUris);
                // Items were removed — reload from the database to stay consistent.
                await RefreshItemsAsync();
                _clipboardMonitor.ResetDuplicateTracking();
                SelectedItem = Items.FirstOrDefault(ci => ci.Id == savedItem.Id) ?? Items.FirstOrDefault();
                return;
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to prune clipboard history.");
        }

        var existing = Items.FirstOrDefault(candidate => candidate.Id == savedItem.Id);
        if (existing is not null)
        {
            Items.Remove(existing);
        }

        await RefreshContentTypeFilterCountsAsync();

        if (!MatchesActiveContentTypeFilter(savedItem))
        {
            SelectedItem = Items.FirstOrDefault();
            return;
        }

        var viewModel = new ClipboardItemViewModel(savedItem);
        Items.Insert(0, viewModel);
        SelectedItem = viewModel;
    }

    private bool MatchesActiveContentTypeFilter(ClipboardItem item)
    {
        if (IsPinnedFilterActive)
        {
            return item.IsPinned;
        }

        return ActiveContentTypeFilter is null || item.ContentType == ActiveContentTypeFilter.Value;
    }

    private async Task RefreshContentTypeFilterCountsAsync()
    {
        try
        {
            var baseItems = await _historyService.SearchAsync(SearchText, CancellationToken.None);
            UpdateContentTypeFilterCounts(baseItems);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to refresh content type filter counts.");
        }
    }
}
