using Clipt.Core.Models;
using Clipt.Core.Services;

namespace Clipt.App.ViewModels;

public sealed class ClipboardFormatBadgeViewModel
{
    public ClipboardFormatBadgeViewModel(ClipboardFormat format)
    {
        Name = format.Name;
        DisplayName = ClipboardFormatHelper.GetFriendlyName(format.Name);
        KindLabel = ClipboardFormatHelper.GetKindLabel(format);
        SizeLabel = ClipboardFormatHelper.FormatByteSize(ClipboardFormatHelper.GetFormatSize(format));
    }

    public string Name { get; }
    public string DisplayName { get; }
    public string KindLabel { get; }
    public string SizeLabel { get; }
}
