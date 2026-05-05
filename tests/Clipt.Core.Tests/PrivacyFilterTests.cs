using Clipt.Core.Models;
using Clipt.Core.Services;
using FluentAssertions;
using Xunit;

namespace Clipt.Core.Tests;

public sealed class PrivacyFilterTests
{
    private readonly PrivacyFilter _filter = new();

    [Fact]
    public void ShouldCapture_NullSettings_AllowsNormalItem()
    {
        var item = CreateItem("hello world", "Notepad");

        _filter.ShouldCapture(item, settings: null).Should().BeTrue();
    }

    [Fact]
    public void ShouldCapture_NullSettings_RejectsEmptyContent()
    {
        var item = CreateItem("   ", "Notepad");

        _filter.ShouldCapture(item, settings: null).Should().BeFalse();
    }

    [Fact]
    public void ShouldCapture_DefaultSettings_AllowsNormalItem()
    {
        var item = CreateItem("clipboard content", "Notepad");
        var settings = new AppSettings();

        _filter.ShouldCapture(item, settings).Should().BeTrue();
    }

    [Fact]
    public void ShouldCapture_IgnoresSourceAppName_CaseInsensitive()
    {
        var item = CreateItem("sensitive content", "PasswordManager");
        var settings = new AppSettings
        {
            IgnoredAppNames = ["passwordmanager"],
        };

        _filter.ShouldCapture(item, settings).Should().BeFalse();
    }

    [Fact]
    public void ShouldCapture_IgnoresSourceAppName_DifferentCase()
    {
        var item = CreateItem("data", "PASSWORdMANAGER");
        var settings = new AppSettings
        {
            IgnoredAppNames = ["PasswordManager"],
        };

        _filter.ShouldCapture(item, settings).Should().BeFalse();
    }

    [Fact]
    public void ShouldCapture_DoesNotIgnoreUnrelatedApp()
    {
        var item = CreateItem("normal text", "Notepad");
        var settings = new AppSettings
        {
            IgnoredAppNames = ["PasswordManager"],
        };

        _filter.ShouldCapture(item, settings).Should().BeTrue();
    }

    [Fact]
    public void ShouldCapture_IgnoresSourceAppPath_CaseInsensitive()
    {
        var item = CreateItem("secret file", "SecretVault", "C:\\Program Files\\SecretVault\\secret.exe");
        var settings = new AppSettings
        {
            IgnoredAppPaths = ["c:\\program files\\secretvault\\secret.exe"],
        };

        _filter.ShouldCapture(item, settings).Should().BeFalse();
    }

    [Fact]
    public void ShouldCapture_IgnoresSourceAppPath_StartsWithDirectory()
    {
        var item = CreateItem("build output", "MSBuild", "C:\\Program Files\\dotnet\\dotnet.exe");
        var settings = new AppSettings
        {
            IgnoredAppPaths = ["C:\\Program Files\\dotnet"],
        };

        _filter.ShouldCapture(item, settings).Should().BeFalse();
    }

    [Fact]
    public void ShouldCapture_EmptyAppPaths_AllowsItem()
    {
        var item = CreateItem("normal text", "Notepad", "C:\\Windows\\notepad.exe");
        var settings = new AppSettings
        {
            IgnoredAppPaths = [],
        };

        _filter.ShouldCapture(item, settings).Should().BeTrue();
    }

    [Fact]
    public void ShouldCapture_IgnoresContentBySubstringMatch()
    {
        var item = CreateItem("super secret password: hunter2", "Notepad");
        var settings = new AppSettings
        {
            IgnoredPatterns = ["secret password"],
        };

        _filter.ShouldCapture(item, settings).Should().BeFalse();
    }

    [Fact]
    public void ShouldCapture_IgnoresContentBySubstring_CaseInsensitive()
    {
        var item = CreateItem("My SECRET token", "Notepad");
        var settings = new AppSettings
        {
            IgnoredPatterns = ["secret"],
        };

        _filter.ShouldCapture(item, settings).Should().BeFalse();
    }

    [Fact]
    public void ShouldCapture_IgnoresTitleBySubstring()
    {
        var item = new ClipboardItem
        {
            Id = Guid.NewGuid(),
            ContentHash = "hash",
            Title = "Confidential document",
            PreviewText = "preview",
            Content = "body",
            ContentType = ContentType.Text,
            SourceAppName = "Notepad",
            CreatedAt = DateTimeOffset.Now,
            ByteSize = 4,
            LastUsedAt = DateTimeOffset.Now,
            UseCount = 0,
            Formats = [],
        };

        var settings = new AppSettings
        {
            IgnoredPatterns = ["confidential"],
        };

        _filter.ShouldCapture(item, settings).Should().BeFalse();
    }

    [Fact]
    public void ShouldCapture_IgnoresPreviewBySubstring()
    {
        var item = new ClipboardItem
        {
            Id = Guid.NewGuid(),
            ContentHash = "hash",
            Title = "Title",
            PreviewText = "this contains a private key",
            Content = "body",
            ContentType = ContentType.Text,
            SourceAppName = "Notepad",
            CreatedAt = DateTimeOffset.Now,
            ByteSize = 4,
            LastUsedAt = DateTimeOffset.Now,
            UseCount = 0,
            Formats = [],
        };

        var settings = new AppSettings
        {
            IgnoredPatterns = ["private key"],
        };

        _filter.ShouldCapture(item, settings).Should().BeFalse();
    }

    [Fact]
    public void ShouldCapture_IgnoresContentByRegexPrefix()
    {
        var item = CreateItem("CC: 4111-1111-1111-1111", "Notepad");
        var settings = new AppSettings
        {
            IgnoredPatterns = ["regex:\\d{4}-\\d{4}-\\d{4}-\\d{4}"],
        };

        _filter.ShouldCapture(item, settings).Should().BeFalse();
    }

    [Fact]
    public void ShouldCapture_RegexPrefix_CaseInsensitive()
    {
        var item = CreateItem("cc: 4111-1111-1111-1111", "Notepad");
        var settings = new AppSettings
        {
            IgnoredPatterns = ["REGEX:\\d{4}-\\d{4}-\\d{4}-\\d{4}"],
        };

        _filter.ShouldCapture(item, settings).Should().BeFalse();
    }

    [Fact]
    public void ShouldCapture_InvalidRegex_DoesNotThrow_And_AllowsCapture()
    {
        var item = CreateItem("normal content", "Notepad");
        var settings = new AppSettings
        {
            IgnoredPatterns = ["regex:[invalid"],
        };

        var act = () => _filter.ShouldCapture(item, settings);
        act.Should().NotThrow();
        act().Should().BeTrue("invalid regex patterns should be silently ignored");
    }

    [Fact]
    public void ShouldCapture_EmptyPatternsList_AllowsItem()
    {
        var item = CreateItem("normal content", "Notepad");
        var settings = new AppSettings
        {
            IgnoredPatterns = [],
        };

        _filter.ShouldCapture(item, settings).Should().BeTrue();
    }

    [Fact]
    public void ShouldCapture_EmptyPatternString_IsSkipped()
    {
        var item = CreateItem("normal content", "Notepad");
        var settings = new AppSettings
        {
            IgnoredPatterns = ["   ", "sensitive"],
        };

        // Whitespace-only pattern should be skipped; "sensitive" doesn't match.
        _filter.ShouldCapture(item, settings).Should().BeTrue();
    }

    [Fact]
    public void ShouldCapture_EmptyRegexBody_IsSkipped()
    {
        var item = CreateItem("normal content", "Notepad");
        var settings = new AppSettings
        {
            IgnoredPatterns = ["regex:  "],
        };

        _filter.ShouldCapture(item, settings).Should().BeTrue();
    }

    [Fact]
    public void ShouldCapture_MultipleIgnoreCriteria_SingleMatchBlocks()
    {
        var item = CreateItem("regular text", "Notepad");
        var settings = new AppSettings
        {
            IgnoredAppNames = ["Rider", "Notepad"],
            IgnoredAppPaths = ["C:\\Secret\\app.exe"],
            IgnoredPatterns = ["credit.card"],
        };

        // SourceAppName "Notepad" matches second ignored app name.
        _filter.ShouldCapture(item, settings).Should().BeFalse();
    }

    [Fact]
    public void ShouldCapture_IgnoreAppPath_WhenSourcePathIsNull_AllowsItem()
    {
        var item = CreateItem("text", "AppName", sourceAppPath: null);
        var settings = new AppSettings
        {
            IgnoredAppPaths = ["C:\\Some\\Path"],
        };

        _filter.ShouldCapture(item, settings).Should().BeTrue();
    }

    private static ClipboardItem CreateItem(
        string content,
        string sourceAppName,
        string? sourceAppPath = null)
    {
        return new ClipboardItem
        {
            Id = Guid.NewGuid(),
            ContentHash = ClipboardContentHasher.ComputeHash(content),
            Title = "Test Item",
            PreviewText = content.Length > 50 ? content[..50] : content,
            Content = content,
            ContentType = ContentType.Text,
            SourceAppName = sourceAppName,
            SourceAppPath = sourceAppPath,
            CreatedAt = DateTimeOffset.Now,
            ByteSize = content.Length,
            LastUsedAt = DateTimeOffset.Now,
            UseCount = 0,
            Formats = [],
        };
    }
}
