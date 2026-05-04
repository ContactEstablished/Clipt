using Clipt.Core.Models;
using Clipt.Core.Services;
using FluentAssertions;
using Xunit;

namespace Clipt.Core.Tests;

public sealed class ContentTypeDetectorTests
{
    private readonly ContentTypeDetector _detector = new();

    [Theory]
    [InlineData("#14B8A6", ContentType.Color)]
    [InlineData("https://github.com/ContactEstablished/Clipt", ContentType.Url)]
    [InlineData("{\"name\":\"Clipt\"}", ContentType.Json)]
    [InlineData("# Heading\n\nSome markdown", ContentType.Markdown)]
    [InlineData("public sealed class ClipboardItem { }", ContentType.Code)]
    [InlineData("just a regular note", ContentType.Text)]
    public void Detect_ReturnsExpectedType(string content, ContentType expected)
    {
        _detector.Detect(content).Should().Be(expected);
    }
}
