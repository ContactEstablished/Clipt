using Clipt.Core.Models;
using Clipt.Core.Services;
using FluentAssertions;
using Xunit;

namespace Clipt.Core.Tests;

public sealed class ClipboardFormatHelperTests
{
    [Theory]
    [InlineData("UnicodeText", "Unicode text")]
    [InlineData("Text", "ANSI text")]
    [InlineData("HTML Format", "HTML")]
    [InlineData("Rich Text Format", "RTF")]
    [InlineData("FileDrop", "File drop")]
    [InlineData("UnknownFormat", "UnknownFormat")]
    [InlineData("", "")]
    public void GetFriendlyName_MapsKnownFormats(string input, string expected)
    {
        ClipboardFormatHelper.GetFriendlyName(input).Should().Be(expected);
    }

    [Fact]
    public void GetFormatSize_TextPayload_ReturnsUtf8ByteCount()
    {
        var format = new ClipboardFormat("Test", TextPayload: "hello");

        ClipboardFormatHelper.GetFormatSize(format).Should().Be(5);
    }

    [Fact]
    public void GetFormatSize_UnicodeText_ReturnsCorrectUtf8Count()
    {
        var format = new ClipboardFormat("UnicodeText", TextPayload: "café");

        ClipboardFormatHelper.GetFormatSize(format).Should().Be(5);
    }

    [Fact]
    public void GetFormatSize_BinaryPayload_ReturnsLength()
    {
        var format = new ClipboardFormat("Binary", TextPayload: null, BinaryPayload: [1, 2, 3, 4, 5]);

        ClipboardFormatHelper.GetFormatSize(format).Should().Be(5);
    }

    [Fact]
    public void GetFormatSize_NoPayload_ReturnsZero()
    {
        var format = new ClipboardFormat("Empty", TextPayload: null, BinaryPayload: null);

        ClipboardFormatHelper.GetFormatSize(format).Should().Be(0);
    }

    [Fact]
    public void GetFormatSize_TextPayloadOverBinaryPayload_PrefersText()
    {
        var format = new ClipboardFormat("Both", TextPayload: "ab", BinaryPayload: [1, 2, 3, 4, 5]);

        ClipboardFormatHelper.GetFormatSize(format).Should().Be(2);
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1, "1 B")]
    [InlineData(512, "512 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(4500, "4.4 KB")]
    [InlineData(1048576, "1.0 MB")]
    [InlineData(1572864, "1.5 MB")]
    public void FormatByteSize_FormatsCorrectly(long bytes, string expected)
    {
        ClipboardFormatHelper.FormatByteSize(bytes).Should().Be(expected);
    }

    [Fact]
    public void GetKindLabel_WithTextPayload_ReturnsText()
    {
        var format = new ClipboardFormat("Test", TextPayload: "hello");

        ClipboardFormatHelper.GetKindLabel(format).Should().Be("Text");
    }

    [Fact]
    public void GetKindLabel_WithoutTextPayload_ReturnsBinary()
    {
        var format = new ClipboardFormat("Test", TextPayload: null, BinaryPayload: [1]);

        ClipboardFormatHelper.GetKindLabel(format).Should().Be("Binary");
    }

    [Fact]
    public void GetKindLabel_NoPayload_ReturnsBinary()
    {
        var format = new ClipboardFormat("Test", TextPayload: null);

        ClipboardFormatHelper.GetKindLabel(format).Should().Be("Binary");
    }
}
