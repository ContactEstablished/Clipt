using Clipt.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clipt.App.ViewModels;

public sealed partial class ContentTypeFilterViewModel : ObservableObject
{
    public ContentTypeFilterViewModel(string label, ContentType? contentType)
    {
        Label = label;
        ContentType = contentType;
    }

    public string Label { get; }

    public ContentType? ContentType { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial int Count { get; set; }
}
