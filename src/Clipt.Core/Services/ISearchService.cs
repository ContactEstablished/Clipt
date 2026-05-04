using Clipt.Core.Models;

namespace Clipt.Core.Services;

public interface ISearchService
{
    IReadOnlyList<ClipboardItem> Filter(IReadOnlyList<ClipboardItem> items, string query);
}
