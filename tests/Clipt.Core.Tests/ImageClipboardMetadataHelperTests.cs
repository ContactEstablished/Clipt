using Clipt.Core.Services;
using FluentAssertions;
using Xunit;

namespace Clipt.Core.Tests;

public sealed class ImageClipboardMetadataHelperTests
{
    [Theory]
    [InlineData(1920, 1080, "Image 1920 x 1080")]
    [InlineData(800, 600, "Image 800 x 600")]
    [InlineData(1, 1, "Image 1 x 1")]
    [InlineData(3840, 2160, "Image 3840 x 2160")]
    public void CreateTitle_ReturnsFormattedTitle(int width, int height, string expected)
    {
        ImageClipboardMetadataHelper.CreateTitle(width, height).Should().Be(expected);
    }

    [Fact]
    public void CreatePreviewText_IncludesDimensionsAndSize()
    {
        var byteSize = ImageClipboardMetadataHelper.EstimateRgbaByteSize(1920, 1080);
        var result = ImageClipboardMetadataHelper.CreatePreviewText(1920, 1080, byteSize);

        result.Should().StartWith("1920 x 1080");
        result.Should().Contain("MB");
    }

    [Theory]
    [InlineData(1920, 1080, 1920L * 1080 * 4)]
    [InlineData(100, 100, 40000L)]
    [InlineData(1, 1, 4L)]
    [InlineData(4096, 4096, 4096L * 4096 * 4)]
    public void EstimateRgbaByteSize_ReturnsWidthTimesHeightTimesFour(int width, int height, long expected)
    {
        ImageClipboardMetadataHelper.EstimateRgbaByteSize(width, height).Should().Be(expected);
    }

    [Theory]
    [InlineData(1280, 720, "1280 x 720")]
    [InlineData(100, 200, "100 x 200")]
    [InlineData(1, 1, "1 x 1")]
    public void FormatPixelSize_ReturnsDimensionString(int width, int height, string expected)
    {
        ImageClipboardMetadataHelper.FormatPixelSize(width, height).Should().Be(expected);
    }

    [Fact]
    public void CreatePreviewText_SmallImage_ShowsBytesOrKilobytes()
    {
        var result = ImageClipboardMetadataHelper.CreatePreviewText(10, 10, 400);
        (result.Contains("B") || result.Contains("KB")).Should().BeTrue();
    }

    [Fact]
    public void CreatePreviewText_LargeImage_ShowsMegabytes()
    {
        var byteSize = ImageClipboardMetadataHelper.EstimateRgbaByteSize(1920, 1080);
        var result = ImageClipboardMetadataHelper.CreatePreviewText(1920, 1080, byteSize);
        result.Should().Contain("MB");
    }

    [Fact]
    public void CreatePreviewText_ContainsDimensionPrefix()
    {
        var result = ImageClipboardMetadataHelper.CreatePreviewText(640, 480, 1_228_800L);
        result.Should().StartWith("640 x 480 - ");
    }
}
