using System.IO;
using Cine.Avalonia.Services;
using Shouldly;
using Xunit;

namespace Cine.Tests.Services;

public class ResumeServiceTests : IDisposable
{
    private readonly string _sessionPath;
    private readonly SessionManager _session;
    private readonly ResumeService _sut;

    public ResumeServiceTests()
    {
        _sessionPath = Path.Combine(Path.GetTempPath(), $"cine_resume_{System.Guid.NewGuid():N}.json");
        _session = new SessionManager(_sessionPath);
        _sut = new ResumeService(_session);
    }

    public void Dispose()
    {
        try { if (System.IO.File.Exists(_sessionPath)) System.IO.File.Delete(_sessionPath); } catch { }
    }

    [Fact]
    public void TryResume_WhenNoSession_ReturnsNull()
    {
        _session.Clear();
        _sut.TryResume().ShouldBeNull();
    }

    [Fact]
    public void TryResume_AfterSave_ReturnsData()
    {
        _session.Save(@"C:\test.mkv", System.TimeSpan.FromSeconds(30), 0, 0, 0, 0, "Auto");
        var result = _sut.TryResume();
        result.ShouldNotBeNull();
        result.FilePath.ShouldBe(@"C:\test.mkv");
    }

    [Fact]
    public void TryResume_CorruptJson_ReturnsNull()
    {
        System.IO.File.WriteAllText(_sessionPath, "not json {{{");
        var result = _sut.TryResume();
        result.ShouldBeNull();
    }

    [Fact]
    public void IsValid_NullData_ReturnsFalse()
    {
        _sut.IsValid(null).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_EmptyFilePath_ReturnsFalse()
    {
        var data = new SessionData("", 0, -1, -1, 0, 0, "Auto");
        _sut.IsValid(data).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_WhitespaceFilePath_ReturnsFalse()
    {
        var data = new SessionData("  ", 0, -1, -1, 0, 0, "Auto");
        _sut.IsValid(data).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_NegativePosition_ReturnsFalse()
    {
        var data = new SessionData(@"C:\test.mkv", -1, -1, -1, 0, 0, "Auto");
        _sut.IsValid(data).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_FileDoesNotExist_ReturnsFalse()
    {
        var data = new SessionData(@"C:\nonexistent_file_9F3A2C7B.mkv",
            0, -1, -1, 0, 0, "Auto");
        _sut.IsValid(data).ShouldBeFalse();
    }
}
