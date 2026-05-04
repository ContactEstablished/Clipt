using Clipt.Core.Models;

namespace Clipt.Core.Services;

public sealed class SearchService : ISearchService
{
    public IReadOnlyList<ClipboardItem> Filter(IReadOnlyList<ClipboardItem> items, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return items;
        }

        return items
            .Where(item =>
                item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.PreviewText.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
