using Clipt.Core.Services;
using FluentAssertions;
using Xunit;

namespace Clipt.Core.Tests;

public sealed class PrivacyEntryNormalizerTests
{
    [Fact]
    public void Normalize_NullInput_ReturnsEmpty()
    {
        PrivacyEntryNormalizer.Normalize(null).Should().BeEmpty();
    }

    [Fact]
    public void Normalize_EmptyInput_ReturnsEmpty()
    {
        PrivacyEntryNormalizer.Normalize([]).Should().BeEmpty();
    }

    [Fact]
    public void Normalize_TrimsWhitespace()
    {
        var result = PrivacyEntryNormalizer.Normalize(["  KeePass  ", "\tNotepad\t"]);

        result.Should().Equal("KeePass", "Notepad");
    }

    [Fact]
    public void Normalize_DropsBlankAndWhitespaceOnlyEntries()
    {
        var result = PrivacyEntryNormalizer.Normalize(["", "   ", "\t", "Real"]);

        result.Should().ContainSingle().Which.Should().Be("Real");
    }

    [Fact]
    public void Normalize_DropsNullEntries()
    {
        // The textbox split path won't produce null today, but the normalizer
        // is the persistence boundary so it should be defensive.
        var entries = new string?[] { "first", null, "second" };

        var result = PrivacyEntryNormalizer.Normalize(entries!);

        result.Should().Equal("first", "second");
    }

    [Fact]
    public void Normalize_DedupesExactDuplicates()
    {
        var result = PrivacyEntryNormalizer.Normalize(["A", "B", "A", "C", "B"]);

        result.Should().Equal("A", "B", "C");
    }

    [Fact]
    public void Normalize_DedupesCaseInsensitively_PreservingFirstCasing()
    {
        // Matcher uses OrdinalIgnoreCase so KeePass and KEEPASS would behave
        // identically; the normalizer should drop the redundant variant but
        // keep the user's first-typed casing.
        var result = PrivacyEntryNormalizer.Normalize(["KeePass", "KEEPASS", "keepass"]);

        result.Should().ContainSingle().Which.Should().Be("KeePass");
    }

    [Fact]
    public void Normalize_DedupesAfterTrimming()
    {
        var result = PrivacyEntryNormalizer.Normalize(["chrome.exe", "  chrome.exe  ", "CHROME.EXE"]);

        result.Should().ContainSingle().Which.Should().Be("chrome.exe");
    }

    [Fact]
    public void Normalize_PreservesInsertionOrder()
    {
        var result = PrivacyEntryNormalizer.Normalize(["zeta", "alpha", "mu", "beta"]);

        result.Should().Equal("zeta", "alpha", "mu", "beta");
    }

    [Fact]
    public void Normalize_PreservesPathPrefixesAsTyped()
    {
        // PrivacyFilter matches paths via OrdinalIgnoreCase Equals or StartsWith.
        // The normalizer must not alter trailing separators, since users may
        // intentionally use prefix matching ("C:\\Users\\me\\Tools\\").
        var entries = new[]
        {
            @"C:\Users\me\Tools\",
            @"C:\Users\me\App.exe",
            @"C:\users\me\tools\",
        };

        var result = PrivacyEntryNormalizer.Normalize(entries);

        result.Should().Equal(@"C:\Users\me\Tools\", @"C:\Users\me\App.exe");
    }

    [Fact]
    public void Normalize_PreservesRegexPrefixedPatternsVerbatim()
    {
        // Pattern handling lives in PrivacyFilter; the normalizer must not
        // interpret the "regex:" prefix or modify the expression.
        var result = PrivacyEntryNormalizer.Normalize(["regex:foo.*bar", "  regex:foo.*bar  "]);

        result.Should().ContainSingle().Which.Should().Be("regex:foo.*bar");
    }
}
