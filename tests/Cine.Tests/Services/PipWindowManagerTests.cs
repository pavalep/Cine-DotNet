using System;
using Cine.Avalonia.Services;
using Cine.Avalonia.ViewModels;
using Cine.Media.Interfaces;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cine.Tests.Services;

public class PipWindowManagerTests
{
    private readonly IPipService _mockPipService;
    private readonly MainViewModel _viewModel;

    public PipWindowManagerTests()
    {
        _mockPipService = Substitute.For<IPipService>();
        var mockPlayer = Substitute.For<IMediaPlayer>();
        _viewModel = new MainViewModel(mockPlayer);
    }

    private PipWindowManager CreateSut(Action<string>? onOsd = null)
    {
        return new PipWindowManager(
            _mockPipService,
            _viewModel, null!, null!, null!, null!, onOsd ?? (_ => { }));
    }

    [Fact]
    public void IsActive_FalseByDefault()
    {
        var sut = CreateSut();
        sut.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void TogglePip_WhenInactive_EntersPip()
    {
        _viewModel.FilePath = "test.mp4";
        _mockPipService.IsActive.Returns(false);
        var mockWindow = Substitute.For<IPipWindow>();
        mockWindow.IsClosed.Returns(false);
        _mockPipService.EnterPip().Returns(mockWindow);

        var sut = CreateSut();
        sut.OnPipToggled(null, EventArgs.Empty);

        _mockPipService.Received(1).EnterPip();
    }

    [Fact]
    public void TogglePip_WhenActive_ExitsPip()
    {
        _mockPipService.IsActive.Returns(true);

        var sut = CreateSut();
        sut.OnPipToggled(null, EventArgs.Empty);

        _mockPipService.Received(1).ExitPip();
    }

    [Fact]
    public void SyncPosition_WhenActive_UpdatesWindow()
    {
        var mockWindow = Substitute.For<IPipWindow>();
        mockWindow.IsClosed.Returns(false);
        _mockPipService.PipWindow.Returns(mockWindow);
        _mockPipService.IsActive.Returns(true);

        var sut = CreateSut();
        var evt = new Cine.Media.Events.PositionChangedEventArgs(
            TimeSpan.FromSeconds(100), TimeSpan.FromSeconds(200));
        sut.SyncPosition(null, evt);

        mockWindow.Received(1).UpdatePosition(100.0, 200.0);
    }

    [Fact]
    public void SyncPlayState_WhenActive_SetsPlayingState()
    {
        var mockWindow = Substitute.For<IPipWindow>();
        mockWindow.IsClosed.Returns(false);
        _mockPipService.PipWindow.Returns(mockWindow);
        _mockPipService.IsActive.Returns(true);

        var sut = CreateSut();
        sut.SyncPlayState(Cine.Media.Models.PlaybackState.Playing);

        mockWindow.Received(1).SetPlayingState(true);
    }

    [Fact]
    public void SyncReplayMode_WhenActive_SetsReplayMode()
    {
        var mockWindow = Substitute.For<IPipWindow>();
        mockWindow.IsClosed.Returns(false);
        _mockPipService.PipWindow.Returns(mockWindow);
        _mockPipService.IsActive.Returns(true);

        var sut = CreateSut();
        sut.SyncReplayMode(true);

        mockWindow.Received(1).SetReplayMode(true);
    }

    [Fact]
    public void Dispose_CallsDisposeOnPipService()
    {
        var sut = CreateSut();
        sut.Dispose();

        _mockPipService.Received(1).Dispose();
    }

    [Fact]
    public void TogglePip_WhenNoFile_DoesNotEnterPip()
    {
        _mockPipService.IsActive.Returns(false);

        var osdMessage = string.Empty;
        var sut = CreateSut(msg => osdMessage = msg);
        sut.OnPipToggled(null, EventArgs.Empty);

        _mockPipService.DidNotReceive().EnterPip();
    }
}
