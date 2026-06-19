using System.Collections.ObjectModel;
using System.Linq;
using Cine.Avalonia.Services;
using Cine.Avalonia.ViewModels;
using Shouldly;
using Xunit;

namespace Cine.Tests.ViewModels;

public class PlaylistDialogHelpersTests
{
    private static PlaylistItemViewModel MakeItem(string path, int index) =>
        new(null!, index, path);

    // ── ApplySearchFilter ───────────────────────────────────────

    [Fact]
    public void ApplySearchFilter_MatchingTitle_SetsVisible()
    {
        var items = new ObservableCollection<PlaylistItemViewModel>
        {
            MakeItem(@"C:\Movie (2024).mkv", 0),
            MakeItem(@"C:\Series S01E01.mkv", 1),
            MakeItem(@"C:\Other.mp4", 2),
        };

        var any = PlaylistDialogHelpers.ApplySearchFilter(items, "movie");

        any.ShouldBeTrue();
        items[0].IsVisible.ShouldBeTrue();
        items[1].IsVisible.ShouldBeFalse();
        items[2].IsVisible.ShouldBeFalse();
    }

    [Fact]
    public void ApplySearchFilter_NoMatch_AllHidden()
    {
        var items = new ObservableCollection<PlaylistItemViewModel>
        {
            MakeItem(@"C:\A.mkv", 0),
        };

        var any = PlaylistDialogHelpers.ApplySearchFilter(items, "zzz_nomatch");

        any.ShouldBeFalse();
        items[0].IsVisible.ShouldBeFalse();
    }

    [Fact]
    public void ApplySearchFilter_EmptyFilter_AllVisible()
    {
        var items = new ObservableCollection<PlaylistItemViewModel>
        {
            MakeItem(@"C:\A.mkv", 0),
            MakeItem(@"C:\B.mkv", 1),
        };

        var any = PlaylistDialogHelpers.ApplySearchFilter(items, "");

        any.ShouldBeTrue();
        items.All(i => i.IsVisible).ShouldBeTrue();
    }

    [Fact]
    public void ApplySearchFilter_CaseInsensitive()
    {
        var items = new ObservableCollection<PlaylistItemViewModel>
        {
            MakeItem(@"C:\Movie.MKV", 0),
        };

        var any = PlaylistDialogHelpers.ApplySearchFilter(items, "movie");

        any.ShouldBeTrue();
        items[0].IsVisible.ShouldBeTrue();
    }

    // ── ExportToM3UAsync ────────────────────────────────────────

    [Fact]
    public async Task ExportToM3U_WritesCorrectFormat()
    {
        var items = new ObservableCollection<PlaylistItemViewModel>
        {
            MakeItem(@"C:\a.mkv", 0),
            MakeItem(@"C:\b.mkv", 1),
        };

        var tempFile = System.IO.Path.GetTempFileName();
        try
        {
            await PlaylistDialogHelpers.ExportToM3UAsync(items, tempFile);

            var lines = await System.IO.File.ReadAllLinesAsync(tempFile);
            lines.Length.ShouldBe(5); // #EXTM3U + 2*(#EXTINF + filepath)
            lines[0].ShouldBe("#EXTM3U");
            lines[1].ShouldBe("#EXTINF:0,a");
            lines[2].ShouldBe(@"C:\a.mkv");
            lines[3].ShouldBe("#EXTINF:0,b");
            lines[4].ShouldBe(@"C:\b.mkv");
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    [Fact]
    public async Task ExportToM3U_EmptyList_OnlyHeader()
    {
        var items = new ObservableCollection<PlaylistItemViewModel>();

        var tempFile = System.IO.Path.GetTempFileName();
        try
        {
            await PlaylistDialogHelpers.ExportToM3UAsync(items, tempFile);

            var lines = await System.IO.File.ReadAllLinesAsync(tempFile);
            lines.Length.ShouldBe(1);
            lines[0].ShouldBe("#EXTM3U");
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }
}
