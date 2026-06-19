using System.IO;
using Cine.Avalonia.Managers;
using Cine.Avalonia.Services;
using Shouldly;
using Xunit;

namespace Cine.Tests.Services;

public class PlaylistCoordinatorTests
{
    private readonly PlaylistCoordinator _sut;

    public PlaylistCoordinatorTests()
    {
        _sut = new PlaylistCoordinator();
    }

    // ── Add ──────────────────────────────────────────────────────

    [Fact]
    public void Add_AddsItem()
    {
        _sut.Add(@"C:\test\video.mp4");
        _sut.Count.ShouldBe(1);
        _sut.Items.ShouldContain(@"C:\test\video.mp4");
    }

    [Fact]
    public void Add_Duplicate_Ignored()
    {
        _sut.Add(@"C:\test\video.mp4");
        _sut.Add(@"C:\test\video.mp4");
        _sut.Count.ShouldBe(1);
    }

    [Fact]
    public void Add_EmptyPath_Ignored()
    {
        _sut.Add("");
        _sut.Count.ShouldBe(0);
    }

    // ── AddRange ─────────────────────────────────────────────────

    [Fact]
    public void AddRange_AddsMultipleItems()
    {
        _sut.AddRange(new[] { @"C:\a.mp4", @"C:\b.mp4", @"C:\c.mp4" });
        _sut.Count.ShouldBe(3);
    }

    // ── RemoveAt ─────────────────────────────────────────────────

    [Fact]
    public void RemoveAt_RemovesItem()
    {
        _sut.AddRange(new[] { @"C:\a.mp4", @"C:\b.mp4", @"C:\c.mp4" });
        _sut.RemoveAt(1);
        _sut.Count.ShouldBe(2);
        _sut.Items.ShouldNotContain(@"C:\b.mp4");
    }

    [Fact]
    public void RemoveAt_InvalidIndex_DoesNothing()
    {
        _sut.RemoveAt(-1);
        _sut.RemoveAt(0);
        Should.NotThrow(() => _sut.RemoveAt(999));
    }

    [Fact]
    public void RemoveAt_AdjustsCurrentIndex()
    {
        _sut.AddRange(new[] { @"C:\a.mp4", @"C:\b.mp4", @"C:\c.mp4" });
        _sut.CurrentIndex = 2;
        _sut.RemoveAt(2);
        _sut.CurrentIndex.ShouldBe(1);
    }

    [Fact]
    public void RemoveAt_WhenLastRemoved_SetsIndexToNegativeOne()
    {
        _sut.Add(@"C:\a.mp4");
        _sut.CurrentIndex = 0;
        _sut.RemoveAt(0);
        _sut.CurrentIndex.ShouldBe(-1);
    }

    // ── Clear ────────────────────────────────────────────────────

    [Fact]
    public void Clear_RemovesAllItems()
    {
        _sut.AddRange(new[] { @"C:\a.mp4", @"C:\b.mp4" });
        _sut.Clear();
        _sut.Count.ShouldBe(0);
        _sut.CurrentIndex.ShouldBe(-1);
    }

    // ── Move ─────────────────────────────────────────────────────

    [Fact]
    public void Move_ChangesOrder()
    {
        _sut.AddRange(new[] { @"C:\a.mp4", @"C:\b.mp4", @"C:\c.mp4" });
        _sut.Move(0, 2);
        _sut.Items[0].ShouldBe(@"C:\b.mp4");
        _sut.Items[1].ShouldBe(@"C:\c.mp4");
        _sut.Items[2].ShouldBe(@"C:\a.mp4");
    }

    // ── CurrentIndex ─────────────────────────────────────────────

    [Fact]
    public void CurrentIndex_ClampsToRange()
    {
        _sut.Add(@"C:\a.mp4");
        _sut.CurrentIndex = 999;
        _sut.CurrentIndex.ShouldBe(0);

        _sut.CurrentIndex = -10;
        _sut.CurrentIndex.ShouldBe(-1);
    }

    [Fact]
    public void CurrentPath_ReturnsCorrectPath()
    {
        _sut.Add(@"C:\a.mp4");
        _sut.CurrentIndex = 0;
        _sut.CurrentPath.ShouldBe(@"C:\a.mp4");
    }

    [Fact]
    public void CurrentPath_WhenEmpty_ReturnsNull()
    {
        _sut.CurrentPath.ShouldBeNull();
    }

    // ── Shuffle ──────────────────────────────────────────────────

    [Fact]
    public void Shuffle_ReordersItems()
    {
        _sut.AddRange(new[] { @"C:\a.mp4", @"C:\b.mp4", @"C:\c.mp4", @"C:\d.mp4", @"C:\e.mp4" });
        _sut.Shuffle();
        _sut.Count.ShouldBe(5);
        _sut.Items.ShouldContain(@"C:\a.mp4");
        _sut.Items.ShouldContain(@"C:\b.mp4");
        _sut.Items.ShouldContain(@"C:\c.mp4");
        _sut.Items.ShouldContain(@"C:\d.mp4");
        _sut.Items.ShouldContain(@"C:\e.mp4");
    }

    // ── SortByTitle ──────────────────────────────────────────────

    [Fact]
    public void SortByTitle_SortsAlphabetically()
    {
        _sut.AddRange(new[] { @"C:\z.mp4", @"C:\a.mp4", @"C:\m.mp4" });
        _sut.SortByTitle();
        _sut.Items[0].ShouldBe(@"C:\a.mp4");
        _sut.Items[1].ShouldBe(@"C:\m.mp4");
        _sut.Items[2].ShouldBe(@"C:\z.mp4");
    }

    // ── GetNextIndex ─────────────────────────────────────────────

    [Fact]
    public void GetNextIndex_Empty_ReturnsNull()
    {
        _sut.GetNextIndex().ShouldBeNull();
    }

    [Fact]
    public void GetNextIndex_AdvancesIndex()
    {
        _sut.AddRange(new[] { @"C:\a.mp4", @"C:\b.mp4", @"C:\c.mp4" });
        _sut.CurrentIndex = 0;

        var next = _sut.GetNextIndex();
        next.ShouldBe(1);
    }

    [Fact]
    public void GetNextIndex_AtEndWithoutLoop_ReturnsNull()
    {
        _sut.AddRange(new[] { @"C:\a.mp4", @"C:\b.mp4" });
        _sut.CurrentIndex = 1;

        _sut.GetNextIndex().ShouldBeNull();
    }

    [Fact]
    public void GetNextIndex_AtEndWithLoop_WrapsToStart()
    {
        _sut.AddRange(new[] { @"C:\a.mp4", @"C:\b.mp4" });
        _sut.CurrentIndex = 1;
        _sut.IsLoopPlaylistEnabled = true;

        _sut.GetNextIndex().ShouldBe(0);
    }

    [Fact]
    public void GetNextIndex_WithShuffle_ReturnsRandom()
    {
        _sut.AddRange(new[] { @"C:\a.mp4", @"C:\b.mp4", @"C:\c.mp4" });
        _sut.IsShuffleEnabled = true;

        var next = _sut.GetNextIndex();
        next.ShouldNotBeNull();
        next.Value.ShouldBeInRange(0, 2);
    }

    // ── GetPreviousIndex ─────────────────────────────────────────

    [Fact]
    public void GetPreviousIndex_Empty_ReturnsNull()
    {
        _sut.GetPreviousIndex().ShouldBeNull();
    }

    [Fact]
    public void GetPreviousIndex_GoesBack()
    {
        _sut.AddRange(new[] { @"C:\a.mp4", @"C:\b.mp4", @"C:\c.mp4" });
        _sut.CurrentIndex = 2;

        _sut.GetPreviousIndex().ShouldBe(1);
    }

    [Fact]
    public void GetPreviousIndex_AtStartWithoutLoop_ReturnsNull()
    {
        _sut.AddRange(new[] { @"C:\a.mp4", @"C:\b.mp4" });
        _sut.CurrentIndex = 0;

        _sut.GetPreviousIndex().ShouldBeNull();
    }

    [Fact]
    public void GetPreviousIndex_AtStartWithLoop_WrapsToEnd()
    {
        _sut.AddRange(new[] { @"C:\a.mp4", @"C:\b.mp4" });
        _sut.CurrentIndex = 0;
        _sut.IsLoopPlaylistEnabled = true;

        _sut.GetPreviousIndex().ShouldBe(1);
    }

    [Fact]
    public void GetPreviousIndex_WithShuffle_ReturnsRandom()
    {
        _sut.AddRange(new[] { @"C:\a.mp4", @"C:\b.mp4", @"C:\c.mp4" });
        _sut.IsShuffleEnabled = true;

        var prev = _sut.GetPreviousIndex();
        prev.ShouldNotBeNull();
        prev.Value.ShouldBeInRange(0, 2);
    }

    // ── Persistence ──────────────────────────────────────────────

    [Fact]
    public void SaveAndLoad_Roundtrip()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"cine_test_playlist_{System.Guid.NewGuid():N}.json");
        try
        {
            var store = new PlaylistSettingsStore(tempFile);
            var sut = new PlaylistCoordinator(store);
            sut.AddRange(new[] { @"C:\a.mp4", @"C:\b.mp4" });
            sut.CurrentIndex = 1;

            sut.Save();

            var loaded = new PlaylistCoordinator(new PlaylistSettingsStore(tempFile));
            loaded.Load().ShouldBeTrue();
            loaded.Count.ShouldBe(2);
            loaded.CurrentIndex.ShouldBe(1);
        }
        finally
        {
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
        }
    }

    [Fact]
    public void ClearPersistence_RemovesSavedData()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"cine_test_playlist_{System.Guid.NewGuid():N}.json");
        try
        {
            var store = new PlaylistSettingsStore(tempFile);
            var sut = new PlaylistCoordinator(store);
            sut.Add(@"C:\a.mp4");
            sut.Save();
            sut.ClearPersistence();

            var loaded = new PlaylistCoordinator(new PlaylistSettingsStore(tempFile));
            loaded.Load().ShouldBeFalse();
        }
        finally
        {
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
        }
    }
}
