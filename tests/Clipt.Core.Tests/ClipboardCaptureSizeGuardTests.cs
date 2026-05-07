using Clipt.Core.Services;
using FluentAssertions;
using Xunit;

namespace Clipt.Core.Tests;

public sealed class ClipboardCaptureSizeGuardTests
{
    [Fact]
    public void IsWithinLimit_BelowLimit_ReturnsTrue()
    {
        ClipboardCaptureSizeGuard.IsWithinLimit(byteSize: 100, maxBytes: 1000).Should().BeTrue();
    }

    [Fact]
    public void IsWithinLimit_ExactlyAtLimit_ReturnsTrue()
    {
        ClipboardCaptureSizeGuard.IsWithinLimit(byteSize: 1000, maxBytes: 1000).Should().BeTrue();
    }

    [Fact]
    public void IsWithinLimit_AboveLimit_ReturnsFalse()
    {
        ClipboardCaptureSizeGuard.IsWithinLimit(byteSize: 1001, maxBytes: 1000).Should().BeFalse();
    }

    [Fact]
    public void IsWithinLimit_ZeroLimit_DisablesCheck()
    {
        ClipboardCaptureSizeGuard.IsWithinLimit(byteSize: long.MaxValue, maxBytes: 0).Should().BeTrue();
    }

    [Fact]
    public void IsWithinLimit_NegativeLimit_DisablesCheck()
    {
        ClipboardCaptureSizeGuard.IsWithinLimit(byteSize: long.MaxValue, maxBytes: -1).Should().BeTrue();
    }

    [Fact]
    public void IsWithinLimit_ZeroByteSize_AlwaysWithinLimit()
    {
        ClipboardCaptureSizeGuard.IsWithinLimit(byteSize: 0, maxBytes: 1).Should().BeTrue();
    }
}
