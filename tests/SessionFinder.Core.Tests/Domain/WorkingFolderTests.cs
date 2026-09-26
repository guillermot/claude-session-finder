using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Tests.Domain;

/// <summary>
/// Pins down the grouping rule measured on a real transcript, where one folder is reported under
/// three different spellings and a subfolder of it under a fourth.
/// </summary>
public sealed class WorkingFolderTests
{
    private const string UpperCaseDrive = @"C:\Work\Project";
    private const string LowerCaseDrive = @"c:\work\project";
    private const string SubFolder = @"C:\Work\Project\src";

    [Fact]
    public void From_CasingDiffers_ProducesTheSameKey()
    {
        var first = WorkingFolder.FromTranscriptCwd(UpperCaseDrive);
        var second = WorkingFolder.FromTranscriptCwd(LowerCaseDrive);

        second.Key.Should().Be(first.Key);
    }

    [Fact]
    public void From_CasingDiffers_KeepsEachSpellingForDisplay()
    {
        var folder = WorkingFolder.FromTranscriptCwd(LowerCaseDrive);

        folder.Display.Should().Be(LowerCaseDrive);
    }

    [Fact]
    public void From_Subfolder_IsNotTheSameFolderAsItsParent()
    {
        var parent = WorkingFolder.FromTranscriptCwd(UpperCaseDrive);
        var child = WorkingFolder.FromTranscriptCwd(SubFolder);

        child.Key.Should().NotBe(parent.Key);
    }

    [Theory]
    [InlineData(@"C:\Work\Project\")]
    [InlineData(@"C:\Work\Project\\")]
    [InlineData("C:\\Work\\Project   ")]
    public void From_TrailingSeparatorsOrSpaces_AreRemovedFromDisplay(string cwd)
    {
        var folder = WorkingFolder.FromTranscriptCwd(cwd);

        folder.Display.Should().Be(UpperCaseDrive);
    }

    [Fact]
    public void From_ForwardSlashes_ShareTheKeyWithBackslashes()
    {
        var windowsStyle = WorkingFolder.FromTranscriptCwd(UpperCaseDrive);
        var posixStyle = WorkingFolder.FromTranscriptCwd("C:/Work/Project");

        posixStyle.Key.Should().Be(windowsStyle.Key);
    }

    [Fact]
    public void From_DriveRoot_KeepsItsSeparator()
    {
        var folder = WorkingFolder.FromTranscriptCwd(@"C:\");

        folder.Display.Should().Be(@"C:\");
    }

    [Fact]
    public void From_FilesystemRoot_KeepsItsSeparator()
    {
        var folder = WorkingFolder.FromTranscriptCwd("/");

        folder.Display.Should().Be("/");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void From_BlankPath_IsUnknown(string? cwd)
    {
        var folder = WorkingFolder.FromTranscriptCwd(cwd);

        folder.Should().BeSameAs(WorkingFolder.Unknown);
    }

    [Fact]
    public void From_UnknownSource_IsUnknownEvenWithAPath()
    {
        var folder = WorkingFolder.From(UpperCaseDrive, FolderSource.Unknown);

        folder.IsKnown.Should().BeFalse();
    }

    [Fact]
    public void From_HistoryFile_RecordsTheFallbackSource()
    {
        var folder = WorkingFolder.FromHistoryFile(UpperCaseDrive);

        folder.Source.Should().Be(FolderSource.HistoryFile);
    }

    [Fact]
    public void Equality_SameSpellingAndSource_AreEqual()
    {
        var first = WorkingFolder.FromTranscriptCwd(UpperCaseDrive);
        var second = WorkingFolder.FromTranscriptCwd(UpperCaseDrive + @"\");

        second.Should().Be(first);
    }
}
