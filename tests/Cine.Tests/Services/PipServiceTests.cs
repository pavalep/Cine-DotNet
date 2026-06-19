using Cine.Avalonia.Services;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cine.Tests.Services;

public class PipServiceTests
{
    private readonly IPipWindow _mockWindow;
    private readonly PipService _sut;

    public PipServiceTests()
    {
        _mockWindow = Substitute.For<IPipWindow>();
        _mockWindow.IsClosed.Returns(false);
        // PipService needs an MpvVideoView — we can't mock a UI control.
        // For these tests we test only the non-frame behaviors (lifecycle, event relay).
        // Create a minimal instance that skips EnterPip's video view subscription.
        _sut = new PipService(null!);
    }

    [Fact]
    public void IsActive_FalseByDefault()
    {
        _sut.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void EnterPip_WithMockWindow_ReturnsWindow()
    {
        var result = _sut.EnterPip(_mockWindow);

        result.ShouldNotBeNull();
        result.ShouldBe(_mockWindow);
    }

    [Fact]
    public void EnterPip_SetsIsActive()
    {
        _sut.EnterPip(_mockWindow);

        _sut.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void EnterPip_ShowsWindow()
    {
        _sut.EnterPip(_mockWindow);

        _mockWindow.Received(1).Show();
    }

    [Fact]
    public void EnterPip_CallsShowAllControlsAndStartHoverTimer()
    {
        _sut.EnterPip(_mockWindow);

        _mockWindow.Received(1).ShowAllControls();
        _mockWindow.Received(1).StartHoverTimer();
    }

    [Fact]
    public void ExitPip_ClosesWindow_ResetsIsActive()
    {
        _sut.EnterPip(_mockWindow);
        _sut.ExitPip();

        _sut.IsActive.ShouldBeFalse();
        _mockWindow.Received(1).Close();
        _mockWindow.Closed -= Arg.Any<EventHandler>();
    }

    [Fact]
    public void EnterPip_Twice_ReturnsSameWindow()
    {
        var first = _sut.EnterPip(_mockWindow);
        var second = _sut.EnterPip(_mockWindow);

        second.ShouldBe(first);
        _mockWindow.Received(1).Show(); // only called once
    }

    [Fact]
    public void PlayPauseRequested_FiresFromWindowEvent()
    {
        var wasCalled = false;
        _sut.PlayPauseRequested += (_, _) => wasCalled = true;

        _sut.EnterPip(_mockWindow);
        _mockWindow.PlayPauseRequested += Raise.Event<EventHandler>(_mockWindow, System.EventArgs.Empty);

        wasCalled.ShouldBeTrue();
    }

    [Fact]
    public void SeekRequested_FiresFromWindowEvent()
    {
        var seekValue = 0.0;
        _sut.SeekRequested += (_, pos) => seekValue = pos;

        _sut.EnterPip(_mockWindow);
        _mockWindow.SeekRequested += Raise.Event<EventHandler<double>>(_mockWindow, 0.5);

        seekValue.ShouldBe(0.5);
    }

    [Fact]
    public void Dispose_CleansUp()
    {
        _sut.EnterPip(_mockWindow);
        _sut.Dispose();

        _sut.IsActive.ShouldBeFalse();
        _mockWindow.Received(1).Close();
    }
}
