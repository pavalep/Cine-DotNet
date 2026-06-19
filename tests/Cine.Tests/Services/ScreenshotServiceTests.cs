using System.IO;
using Cine.Avalonia.Services;
using Shouldly;
using Xunit;

namespace Cine.Tests.Services;

public class ScreenshotServiceTests
{
    private readonly ScreenshotService _sut;

    public ScreenshotServiceTests()
    {
        _sut = new ScreenshotService(Path.GetTempPath());
    }

    [Fact]
    public void SaveScreenshot_CallsTakeScreenshotAction()
    {
        var captured = false;

        _sut.SaveScreenshot(() =>
        {
            captured = true;
            return string.Empty;
        });

        captured.ShouldBeTrue();
    }

    [Fact]
    public void SaveScreenshot_ReturnsPath()
    {
        var result = _sut.SaveScreenshot(() => string.Empty);

        result.ShouldNotBeNull();
        result.ShouldEndWith(".png");
    }

    [Fact]
    public void SaveScreenshot_WithNullFormat_DefaultsToPng()
    {
        var result = _sut.SaveScreenshot(() => string.Empty, null);
        result.ShouldEndWith(".png");
    }

    [Fact]
    public void SaveScreenshot_WithInvalidFormat_FallsBackToPng()
    {
        var result = _sut.SaveScreenshot(() => string.Empty, ".gif");
        result.ShouldEndWith(".png");
    }

    [Fact]
    public void SaveScreenshot_WithValidFormat_UsesFormat()
    {
        var result = _sut.SaveScreenshot(() => string.Empty, ".jpg");
        result.ShouldEndWith(".jpg");
    }

    [Fact]
    public void SaveScreenshot_WithoutLeadingDot_NormalizesFormat()
    {
        var result = _sut.SaveScreenshot(() => string.Empty, "jpg");
        result.ShouldEndWith(".jpg");
    }
}
