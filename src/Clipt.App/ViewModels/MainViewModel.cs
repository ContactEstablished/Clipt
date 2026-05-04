using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using Clipt.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clipt.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IHistoryService _historyService;

    public MainViewModel(IHistoryService historyService)
    {
        _historyService = historyService;
        ItemsView = CollectionViewSource.GetDefaultView(Items);
        ItemsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ClipboardItemViewModel.GroupName)));
        ItemsView.SortDescriptions.Add(new SortDescription(nameof(ClipboardItemViewModel.IsPinned), ListSortDirection.Descending));
        ItemsView.SortDescriptions.Add(new SortDescription(nameof(ClipboardItemViewModel.CreatedAt), ListSortDirection.Descending));
        ItemsView.Filter = FilterItem;
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
        foreach (var item in items)
        {
            Items.Add(new ClipboardItemViewModel(item));
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

    partial void OnSearchTextChanged(string value)
    {
        ItemsView.Refresh();
    }

    partial void OnIsWorkModeChanged(bool value)
    {
        OnPropertyChanged(nameof(ModeLabel));
        OnPropertyChanged(nameof(TargetWidth));
    }

    private bool FilterItem(object candidate)
    {
        if (candidate is not ClipboardItemViewModel item)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        return item.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || item.PreviewText.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || item.Content.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }
}
