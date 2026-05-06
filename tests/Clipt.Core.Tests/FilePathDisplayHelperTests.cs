using Clipt.Core.Services;
using FluentAssertions;
using Xunit;

namespace Clipt.Core.Tests;

public sealed class FilePathDisplayHelperTests : IDisposable
{
    private readonly string _tempFile;
    private readonly string _tempDir;
    private bool _disposed;

    public FilePathDisplayHelperTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"clipt-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _tempFile = Path.Combine(_tempDir, "test-file.txt");
        File.WriteAllText(_tempFile, "hello");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (File.Exists(_tempFile))
            {
                File.Delete(_tempFile);
            }

            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }

    [Theory]
    [InlineData(@"C:\Users\Matt\file.txt", "file.txt")]
    [InlineData(@"D:\Projects\Clipt\src\Program.cs", "Program.cs")]
    [InlineData(@"C:\data\image.png", "image.png")]
    [InlineData(@"C:\folder\", "folder")]
    [InlineData(@"/home/user/docs/readme.md", "readme.md")]
    [InlineData(@"\\server\share\path\to\file.log", "file.log")]
    public void GetFileName_ReturnsLeafName(string path, string expected)
    {
        FilePathDisplayHelper.GetFileName(path).Should().Be(expected);
    }

    [Fact]
    public void GetFileName_OnlyRoot_ReturnsEmpty()
    {
        FilePathDisplayHelper.GetFileName(@"C:\").Should().BeEmpty();
    }

    [Theory]
    [InlineData(@"C:\Users\Matt\file.txt", @"C:\Users\Matt")]
    [InlineData(@"D:\Projects\Clipt\src", @"D:\Projects\Clipt")]
    [InlineData(@"C:\folder\", @"C:")]
    [InlineData(@"/home/user/docs/readme.md", @"/home/user/docs")]
    public void GetParentPath_ReturnsDirectoryAbove(string path, string expected)
    {
        FilePathDisplayHelper.GetParentPath(path).Should().Be(expected);
    }

    [Fact]
    public void GetParentPath_RootFile_ReturnsDriveRoot()
    {
        FilePathDisplayHelper.GetParentPath(@"C:\file.txt").Should().Be("C:");
    }

    [Theory]
    [InlineData(@"C:\file.png", "PNG")]
    [InlineData(@"C:\path\script.CS", "CS")]
    [InlineData(@"C:\readme.md", "MD")]
    [InlineData(@"C:\archive.tar.gz", "GZ")]
    public void GetExtensionLabel_ReturnsUppercase(string path, string expected)
    {
        FilePathDisplayHelper.GetExtensionLabel(path).Should().Be(expected);
    }

    [Fact]
    public void GetExtensionLabel_NoExtensionFilename_ReturnsFolder()
    {
        FilePathDisplayHelper.GetExtensionLabel(@"C:\Dockerfile").Should().Be("Folder");
        FilePathDisplayHelper.GetExtensionLabel(@"C:\Makefile").Should().Be("Folder");
    }

    [Theory]
    [InlineData(@"C:\folder\subdir")]
    [InlineData(@"C:\folder\subdir\")]
    [InlineData(@"C:\noext")]
    public void GetExtensionLabel_NoExtension_ReturnsFolder(string path)
    {
        FilePathDisplayHelper.GetExtensionLabel(path).Should().Be("Folder");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetFileName_NullOrWhitespace_ReturnsEmpty(string? path)
    {
        FilePathDisplayHelper.GetFileName(path!).Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetParentPath_NullOrWhitespace_ReturnsEmpty(string? path)
    {
        FilePathDisplayHelper.GetParentPath(path!).Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetExtensionLabel_NullOrWhitespace_ReturnsFolder(string? path)
    {
        FilePathDisplayHelper.GetExtensionLabel(path!).Should().Be("Folder");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetKindLabel_NullOrWhitespace_ReturnsFolder(string? path)
    {
        FilePathDisplayHelper.GetKindLabel(path!).Should().Be("Folder");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Exists_NullOrWhitespace_ReturnsFalse(string? path)
    {
        FilePathDisplayHelper.Exists(path!).Should().BeFalse();
    }

    [Fact]
    public void FormatCountSummary_Singular_ReturnsOneItem()
    {
        FilePathDisplayHelper.FormatCountSummary(["C:\\one.txt"])
            .Should().Be("1 item");
    }

    [Fact]
    public void FormatCountSummary_Plural_ReturnsCount()
    {
        FilePathDisplayHelper.FormatCountSummary(["C:\\a.txt", "D:\\b.txt", "E:\\c.txt"])
            .Should().Be("3 items");
    }

    [Fact]
    public void FormatCountSummary_Null_ReturnsEmpty()
    {
        FilePathDisplayHelper.FormatCountSummary(null).Should().BeEmpty();
    }

    [Fact]
    public void FormatCountSummary_Empty_ReturnsEmpty()
    {
        FilePathDisplayHelper.FormatCountSummary([]).Should().BeEmpty();
    }

    [Fact]
    public void Exists_ExistingFile_ReturnsTrue()
    {
        FilePathDisplayHelper.Exists(_tempFile).Should().BeTrue();
    }

    [Fact]
    public void Exists_ExistingDirectory_ReturnsTrue()
    {
        FilePathDisplayHelper.Exists(_tempDir).Should().BeTrue();
    }

    [Fact]
    public void Exists_NonExisting_ReturnsFalse()
    {
        FilePathDisplayHelper.Exists(@"C:\does-not-exist-at-all-12345.txt").Should().BeFalse();
    }

    [Fact]
    public void GetKindLabel_ExistingFile_ReturnsFile()
    {
        FilePathDisplayHelper.GetKindLabel(_tempFile).Should().Be("File");
    }

    [Fact]
    public void GetKindLabel_ExistingDirectory_ReturnsFolder()
    {
        FilePathDisplayHelper.GetKindLabel(_tempDir).Should().Be("Folder");
    }

    [Fact]
    public void GetKindLabel_NonexistentWithExtension_ReturnsFile()
    {
        FilePathDisplayHelper.GetKindLabel(@"C:\nonexistent\file.txt").Should().Be("File");
    }

    [Fact]
    public void GetKindLabel_NonexistentWithoutExtension_ReturnsFolder()
    {
        FilePathDisplayHelper.GetKindLabel(@"C:\nonexistent\folder").Should().Be("Folder");
    }

    // IsDirectoryPath

    [Fact]
    public void IsDirectoryPath_ExistingFile_ReturnsFalse()
    {
        FilePathDisplayHelper.IsDirectoryPath(_tempFile).Should().BeFalse();
    }

    [Fact]
    public void IsDirectoryPath_ExistingDirectory_ReturnsTrue()
    {
        FilePathDisplayHelper.IsDirectoryPath(_tempDir).Should().BeTrue();
    }

    [Theory]
    [InlineData(@"C:\nonexistent\folder")]
    [InlineData(@"C:\nonexistent\noext")]
    public void IsDirectoryPath_NonexistentNoExtension_ReturnsTrue(string path)
    {
        FilePathDisplayHelper.IsDirectoryPath(path).Should().BeTrue();
    }

    [Theory]
    [InlineData(@"C:\nonexistent\file.txt")]
    [InlineData(@"C:\nonexistent\archive.tar.gz")]
    public void IsDirectoryPath_NonexistentWithExtension_ReturnsFalse(string path)
    {
        FilePathDisplayHelper.IsDirectoryPath(path).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsDirectoryPath_NullOrWhitespace_ReturnsFalse(string? path)
    {
        FilePathDisplayHelper.IsDirectoryPath(path!).Should().BeFalse();
    }

    // GetExplorerArgument

    [Fact]
    public void GetExplorerArgument_ExistingFile_ReturnsSelectArgument()
    {
        var result = FilePathDisplayHelper.GetExplorerArgument(_tempFile);
        result.Should().Be($"/select,\"{_tempFile}\"");
    }

    [Fact]
    public void GetExplorerArgument_ExistingDirectory_ReturnsQuotedPath()
    {
        var result = FilePathDisplayHelper.GetExplorerArgument(_tempDir);
        result.Should().Be($"\"{_tempDir}\"");
    }

    [Fact]
    public void GetExplorerArgument_NonexistentPath_ReturnsEmpty()
    {
        FilePathDisplayHelper.GetExplorerArgument(@"C:\does-not-exist-at-all-12345.txt")
            .Should().BeEmpty();
    }

    [Fact]
    public void GetExplorerArgument_NonexistentDirectory_ReturnsEmpty()
    {
        FilePathDisplayHelper.GetExplorerArgument(@"C:\does-not-exist-folder-99999")
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetExplorerArgument_NullOrWhitespace_ReturnsEmpty(string? path)
    {
        FilePathDisplayHelper.GetExplorerArgument(path!).Should().BeEmpty();
    }
}
