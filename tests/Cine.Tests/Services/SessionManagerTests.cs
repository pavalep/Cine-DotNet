using System;
using System.IO;
using Cine.Avalonia.Services;
using Shouldly;
using Xunit;

namespace Cine.Tests.Services;

public class SessionManagerTests : IDisposable
{
    private readonly string _sessionPath;
    private readonly SessionManager _sut;

    public SessionManagerTests()
    {
        _sessionPath = Path.Combine(Path.GetTempPath(), $"cine_session_{Guid.NewGuid():N}.json");
        _sut = new SessionManager(_sessionPath);
    }

    public void Dispose()
    {
        try { if (File.Exists(_sessionPath)) File.Delete(_sessionPath); } catch { }
    }

    [Fact]
    public void SaveAndLoad_Roundtrip_PreservesData()
    {
        _sut.Save(@"C:\test\movie.mkv", TimeSpan.FromSeconds(42.5), 2, 1, 0.5f, -0.3f, "Hardware");
        var data = _sut.Load();
        data.ShouldNotBeNull();
        data.FilePath.ShouldBe(@"C:\test\movie.mkv");
        data.PositionTicks.ShouldBe(TimeSpan.FromSeconds(42.5).Ticks);
        data.RendererMode.ShouldBe("Hardware");
    }

    [Fact]
    public void Save_DoesNotLeaveTempFile()
    {
        _sut.Save(@"C:\a.mkv", TimeSpan.Zero, -1, -1, 0, 0, "Auto");
        File.Exists(_sessionPath + ".tmp").ShouldBeFalse();
    }

    [Fact]
    public void Load_WhenFileMissing_ReturnsNull()
    {
        _sut.Clear();
        _sut.Load().ShouldBeNull();
    }

    [Fact]
    public void Clear_RemovesSessionFile()
    {
        _sut.Save(@"C:\b.mkv", TimeSpan.FromSeconds(10), 0, 0, 0, 0, "Auto");
        _sut.Clear();
        _sut.Load().ShouldBeNull();
    }

    [Fact]
    public void Load_CorruptJson_ReturnsNull()
    {
        File.WriteAllText(_sessionPath, "this is not valid json {{{");
        _sut.Load().ShouldBeNull();
    }

    [Fact]
    public void SessionData_Construction_PreservesAllFields()
    {
        var data = new SessionData(@"C:\f.mkv", 12345L, 1, 2, 0.5f, -0.3f, "Software");
        data.FilePath.ShouldBe(@"C:\f.mkv");
        data.PositionTicks.ShouldBe(12345L);
        data.AudioTrackId.ShouldBe(2);
        data.SubtitleDelay.ShouldBe(0.5f);
    }
}
