using System.Threading.Tasks;
using Cine.Avalonia.Controls;
using Cine.Tests.Infrastructure;
using Xunit;

namespace Cine.Tests.Headless;

[Collection("Headless")]
public class SubtitleOverlayHeadlessTests
{
    private readonly HeadlessFixture _headless;

    public SubtitleOverlayHeadlessTests(HeadlessFixture headless)
    {
        _headless = headless;
    }

    /// <summary>
    /// Verifies the control can be constructed without throwing.
    /// Confirms that XAML compiles and InitializeComponent() succeeds.
    /// </summary>
    [Fact]
    public async Task Constructor_DoesNotThrow()
    {
        await _headless.DispatchAsync(() =>
        {
            var control = new SubtitleOverlayControl();
            Assert.NotNull(control);
        });
    }
}
