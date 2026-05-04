using Clipt.Core;
using Clipt.Core.Models;
using FluentAssertions;
using Xunit;

namespace Clipt.Core.Tests;

public sealed class DesignTimeDataTests
{
    [Fact]
    public void GetSampleItems_ReturnsExpectedContentTypes()
    {
        var items = DesignTimeData.GetSampleItems();

        items.Should().HaveCount(10);
        items.Select(item => item.ContentType).Should().Contain(
        [
            ContentType.Markdown,
            ContentType.Code,
            ContentType.Json,
            ContentType.Url,
            ContentType.Image,
            ContentType.File,
            ContentType.Color,
            ContentType.Text,
        ]);
        items.Should().Contain(item => item.IsPinned);
    }
}
