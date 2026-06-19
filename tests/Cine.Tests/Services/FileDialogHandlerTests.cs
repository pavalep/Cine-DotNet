using System;
using Cine.Avalonia.Services;
using Shouldly;
using Xunit;

namespace Cine.Tests.Services;

/// <summary>
/// Pure unit tests for <see cref="FileDialogHandler"/> that don't require
/// the Avalonia headless platform.
/// </summary>
public class FileDialogHandlerTests
{
    // ═══════════════════════════════════════════════════
    //  Static Filter Definitions
    // ═══════════════════════════════════════════════════

    [Fact]
    public void VideoFilter_ShouldHavePatterns()
    {
        var filter = FileDialogHandler.VideoFilter;
        filter.ShouldNotBeNull();
        filter.Patterns.ShouldNotBeNull();
        filter.Patterns.Count.ShouldBeGreaterThan(0);
        filter.Patterns.ShouldContain("*.mp4");
        filter.Patterns.ShouldContain("*.mkv");
        filter.Patterns.ShouldContain("*.avi");
    }

    [Fact]
    public void SubtitleFilter_ShouldHavePatterns()
    {
        var filter = FileDialogHandler.SubtitleFilter;
        filter.ShouldNotBeNull();
        filter.Patterns.ShouldNotBeNull();
        filter.Patterns.Count.ShouldBeGreaterThan(0);
        filter.Patterns.ShouldContain("*.srt");
        filter.Patterns.ShouldContain("*.ass");
    }

    [Fact]
    public void AudioFilter_ShouldHavePatterns()
    {
        var filter = FileDialogHandler.AudioFilter;
        filter.ShouldNotBeNull();
        filter.Patterns.ShouldNotBeNull();
        filter.Patterns.Count.ShouldBeGreaterThan(0);
        filter.Patterns.ShouldContain("*.mp3");
        filter.Patterns.ShouldContain("*.flac");
    }

    // ═══════════════════════════════════════════════════
    //  Constructor
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Constructor_WhenTopLevelNull_ShouldThrow()
    {
        Should.Throw<ArgumentNullException>(() => new FileDialogHandler(null!));
    }

    // ═══════════════════════════════════════════════════
    //  File Type Filter Exclusivity
    // ═══════════════════════════════════════════════════

    [Fact]
    public void VideoFilter_ShouldNotContainAudioPatterns()
    {
        var patterns = FileDialogHandler.VideoFilter.Patterns!;
        patterns.ShouldNotContain("*.mp3");
        patterns.ShouldNotContain("*.flac");
    }

    [Fact]
    public void SubtitleFilter_ShouldNotContainVideoPatterns()
    {
        var patterns = FileDialogHandler.SubtitleFilter.Patterns!;
        patterns.ShouldNotContain("*.mp4");
        patterns.ShouldNotContain("*.mkv");
    }
}
