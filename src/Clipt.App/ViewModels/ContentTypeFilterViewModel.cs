using Clipt.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clipt.App.ViewModels;

public sealed partial class ContentTypeFilterViewModel : ObservableObject
{
    public ContentTypeFilterViewModel(string label, ContentType? contentType, bool isPinnedFilter = false)
    {
        Label = label;
        ContentType = contentType;
        IsPinnedFilter = isPinnedFilter;
    }

    public string Label { get; }

    public ContentType? ContentType { get; }

    public bool IsPinnedFilter { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial int Count { get; set; }
}
