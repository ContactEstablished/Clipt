using Clipt.Core;
using Clipt.Core.Models;
using Clipt.Core.Services;
using FluentAssertions;
using Xunit;

namespace Clipt.Core.Tests;

public sealed class DesignTimeDataTests
{
    [Fact]
    public void GetSampleItems_HasMinimumItemCount()
    {
        var items = DesignTimeData.GetSampleItems();

        items.Should().HaveCountGreaterThan(12);
    }

    [Fact]
    public void GetSampleItems_IncludesAllRequiredContentTypes()
    {
        var items = DesignTimeData.GetSampleItems();

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
    }

    [Fact]
    public void GetSampleItems_IncludesSqlAndTypeScriptCodeSamples()
    {
        var items = DesignTimeData.GetSampleItems();

        var codeItems = items.Where(i => i.ContentType == ContentType.Code).ToList();
        codeItems.Should().HaveCountGreaterThan(2);
        codeItems.Should().Contain(i => i.Language == "C#");
        codeItems.Should().Contain(i => i.Language == "TypeScript");
        codeItems.Should().Contain(i => i.Language == "SQL");
    }

    [Fact]
    public void GetSampleItems_HasAtLeastOnePinnedItem()
    {
        var items = DesignTimeData.GetSampleItems();

        items.Should().Contain(item => item.IsPinned);
    }

    [Fact]
    public void GetSampleItems_HasAtLeastTwoPinnedItems()
    {
        var items = DesignTimeData.GetSampleItems();

        items.Count(i => i.IsPinned).Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void GetSampleItems_AllTitlesAreNonEmpty()
    {
        var items = DesignTimeData.GetSampleItems();

        items.Should().AllSatisfy(item => item.Title.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public void GetSampleItems_AllPreviewsAreNonEmpty()
    {
        var items = DesignTimeData.GetSampleItems();

        items.Should().AllSatisfy(item => item.PreviewText.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public void GetSampleItems_AllContentIsNonEmpty()
    {
        var items = DesignTimeData.GetSampleItems();

        items.Should().AllSatisfy(item => item.Content.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public void GetSampleItems_ContentHashesAreUnique()
    {
        var items = DesignTimeData.GetSampleItems();

        var hashes = items.Select(i => i.ContentHash).ToList();
        hashes.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void GetSampleItems_HashesMatchComputedValues()
    {
        var items = DesignTimeData.GetSampleItems();

        foreach (var item in items)
        {
            var recomputed = ClipboardContentHasher.ComputeHash(item.Content);
            item.ContentHash.Should().Be(recomputed,
                "the stored ContentHash for '{0}' should match SHA-256 of its Content", item.Title);
        }
    }

    [Fact]
    public void GetSampleItems_HasRealisticSourceApps()
    {
        var items = DesignTimeData.GetSampleItems();

        var allSources = items.Select(i => i.SourceAppName).ToList();
        allSources.Should().Contain(["VS Code", "Rider", "Figma", "Obsidian"]);
    }

    [Fact]
    public void GetSampleItems_IncludesJsonItems()
    {
        var items = DesignTimeData.GetSampleItems();

        var jsonItems = items.Where(i => i.ContentType == ContentType.Json).ToList();
        jsonItems.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void GetSampleItems_IncludesUrlItems()
    {
        var items = DesignTimeData.GetSampleItems();

        var urlItems = items.Where(i => i.ContentType == ContentType.Url).ToList();
        urlItems.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void GetSampleItems_IncludesColorItems()
    {
        var items = DesignTimeData.GetSampleItems();

        var colorItems = items.Where(i => i.ContentType == ContentType.Color).ToList();
        colorItems.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void GetSampleItems_CreatedAtValuesAreStaggered()
    {
        var items = DesignTimeData.GetSampleItems();

        var createdAtValues = items.Select(i => i.CreatedAt).ToList();
        createdAtValues.Should().OnlyHaveUniqueItems(
            "each demo item should have a distinct CreatedAt for a natural-looking timeline");
    }
}
