using Clipt.Core;
using Clipt.Core.Services;
using FluentAssertions;
using Xunit;

namespace Clipt.Core.Tests;

public sealed class SearchServiceTests
{
    [Fact]
    public void Filter_ReturnsItemsMatchingTitlePreviewOrContent()
    {
        var service = new SearchService();
        var items = DesignTimeData.GetSampleItems();

        var results = service.Filter(items, "FTS5");

        results.Should().ContainSingle();
        results[0].Title.Should().Be("SQL search prototype");
    }

    [Fact]
    public void Filter_WithBlankQuery_ReturnsOriginalItems()
    {
        var service = new SearchService();
        var items = DesignTimeData.GetSampleItems();

        var results = service.Filter(items, " ");

        results.Should().BeSameAs(items);
    }
}
