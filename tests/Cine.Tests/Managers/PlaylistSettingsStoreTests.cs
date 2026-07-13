using System;
using System.IO;
using Cine.Avalonia.Storage;
using Shouldly;
using Xunit;

namespace Cine.Tests.Managers;

public class PlaylistSettingsStoreTests : IDisposable
{
    private readonly PlaylistSettingsStore _sut;
    private readonly string _testFilePath;

    public PlaylistSettingsStoreTests()
    {
        // Use a temp file to avoid %LOCALAPPDATA% sandbox restrictions
        _testFilePath = Path.Combine(Path.GetTempPath(), $"cine_test_playlist_{System.Guid.NewGuid():N}.json");
        _sut = new PlaylistSettingsStore(_testFilePath);
    }

    public void Dispose()
    {
        // Clean up temp file after test
        try { if (File.Exists(_testFilePath)) File.Delete(_testFilePath); } catch { }
    }

    // ── Save / Load ─────────────────────────────────────────────

    [Fact]
    public void SaveAndLoad_Roundtrip()
    {
        var items = new[] { @"C:\a.mp4", @"C:\b.mp4", @"C:\c.mp4" };

        _sut.SavePlaylist(items, 1);

        var loaded = _sut.LoadPlaylist(out var position);
        loaded.ShouldNotBeNull();
        loaded.Count.ShouldBe(3);
        loaded[0].ShouldBe(@"C:\a.mp4");
        loaded[1].ShouldBe(@"C:\b.mp4");
        loaded[2].ShouldBe(@"C:\c.mp4");
        position.ShouldBe(1);
    }

    [Fact]
    public void Load_WhenNoFile_ReturnsNull()
    {
        // Fresh store with no file yet
        var fresh = new PlaylistSettingsStore(
            Path.Combine(Path.GetTempPath(), $"cine_test_nonexistent_{System.Guid.NewGuid():N}.json"));

        var loaded = fresh.LoadPlaylist(out var position);
        loaded.ShouldBeNull();
        position.ShouldBe(-1);
    }

    [Fact]
    public void SaveAndLoad_EmptyList()
    {
        _sut.SavePlaylist(System.Array.Empty<string>(), -1);

        var loaded = _sut.LoadPlaylist(out var position);
        loaded.ShouldBeNull();
        position.ShouldBe(-1);
    }

    [Fact]
    public void SaveAndLoad_ClampsNegativePosition()
    {
        _sut.SavePlaylist(new[] { @"C:\a.mp4" }, -5);

        var loaded = _sut.LoadPlaylist(out var position);
        loaded.ShouldNotBeNull();
        position.ShouldBe(0); // clamped to 0
    }

    // ── ClearPlaylist ────────────────────────────────────────────

    [Fact]
    public void ClearPlaylist_RemovesSavedData()
    {
        _sut.SavePlaylist(new[] { @"C:\a.mp4" }, 0);
        _sut.ClearPlaylist();

        var loaded = _sut.LoadPlaylist(out var position);
        loaded.ShouldBeNull();
    }

    [Fact]
    public void ClearPlaylist_WhenNoData_DoesNotThrow()
    {
        Should.NotThrow(() => _sut.ClearPlaylist());
    }
}
