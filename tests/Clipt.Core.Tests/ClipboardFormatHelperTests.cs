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

    // ── GetPayloadByteCount / SumPayloadBytes ──────────────────────────────

    [Fact]
    public void GetPayloadByteCount_TextOnly_ReturnsUtf8Bytes()
    {
        var format = new ClipboardFormat("UnicodeText", TextPayload: "héllo");

        // 'h','e','l','l','l','o' UTF-8: h=1, é=2, l=1, l=1, o=1 → 6
        ClipboardFormatHelper.GetPayloadByteCount(format).Should().Be(6);
    }

    [Fact]
    public void GetPayloadByteCount_BinaryOnly_ReturnsLength()
    {
        var format = new ClipboardFormat("Bitmap", TextPayload: null, BinaryPayload: new byte[42]);

        ClipboardFormatHelper.GetPayloadByteCount(format).Should().Be(42);
    }

    [Fact]
    public void GetPayloadByteCount_BothPayloads_SumsBoth()
    {
        // Unlike GetFormatSize (display semantics: text wins), GetPayloadByteCount
        // sums both because both bytes are persisted.
        var format = new ClipboardFormat("Hybrid", TextPayload: "ab", BinaryPayload: [1, 2, 3]);

        ClipboardFormatHelper.GetPayloadByteCount(format).Should().Be(5);
    }

    [Fact]
    public void GetPayloadByteCount_NullPayloads_ReturnsZero()
    {
        var format = new ClipboardFormat("Empty", TextPayload: null, BinaryPayload: null);

        ClipboardFormatHelper.GetPayloadByteCount(format).Should().Be(0);
    }

    [Fact]
    public void GetPayloadByteCount_EmptyText_ReturnsZero()
    {
        var format = new ClipboardFormat("Empty", TextPayload: string.Empty);

        ClipboardFormatHelper.GetPayloadByteCount(format).Should().Be(0);
    }

    [Fact]
    public void SumPayloadBytes_EmptyList_ReturnsZero()
    {
        ClipboardFormatHelper.SumPayloadBytes(Array.Empty<ClipboardFormat>()).Should().Be(0);
    }

    [Fact]
    public void SumPayloadBytes_MixedFormats_AccumulatesAcrossList()
    {
        // Realistic browser copy: small Unicode text, larger HTML and RTF payloads.
        var formats = new ClipboardFormat[]
        {
            new("UnicodeText", TextPayload: "hi"),
            new("HTML Format", TextPayload: new string('h', 100)),
            new("Rich Text Format", TextPayload: new string('r', 250)),
        };

        // 2 + 100 + 250 = 352
        ClipboardFormatHelper.SumPayloadBytes(formats).Should().Be(352);
    }

    [Fact]
    public void SumPayloadBytes_GuardsAgainstHtmlAsymmetry()
    {
        // Regression guard for the bug the size guard previously had: a small
        // Unicode text payload with a much larger HTML/RTF companion would have
        // slipped through a unicode-only check.
        var formats = new ClipboardFormat[]
        {
            new("UnicodeText", TextPayload: "tiny"),
            new("HTML Format", TextPayload: new string('h', 5_000_000)),
        };

        ClipboardFormatHelper.SumPayloadBytes(formats).Should().BeGreaterThanOrEqualTo(5_000_000);
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
