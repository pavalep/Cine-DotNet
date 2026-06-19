using System.Threading.Tasks;
using Cine.Avalonia.Controls;
using Cine.Tests.Infrastructure;
using Xunit;

namespace Cine.Tests.Headless;

[Collection("Headless")]
public class OsdNotificationHeadlessTests
{
    private readonly HeadlessFixture _headless;

    public OsdNotificationHeadlessTests(HeadlessFixture headless)
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
            var control = new OsdNotificationControl();
            Assert.NotNull(control);
        });
    }
}
