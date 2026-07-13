using Cine.Avalonia.Services;
using Cine.Media.Interfaces;
using Cine.Media.Models;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cine.Tests.Services;

public class PlayerServiceTests
{
    private readonly IPlayerFactory _factory;
    private readonly IMediaPlayer _mockPlayer;
    private readonly PlayerService _sut;

    public PlayerServiceTests()
    {
        _mockPlayer = Substitute.For<IMediaPlayer>();
        _factory = Substitute.For<IPlayerFactory>();
        _factory.CreatePlayer().Returns(_mockPlayer);
        var codecProvider = Substitute.For<ICodecProvider>();
        codecProvider.IsAvailable.Returns(true);
        codecProvider.Name.Returns("Test");
        codecProvider.GetCapabilities().Returns([new CodecCapability { Codec = "h264" }]);
        var codecManager = new CodecManager([codecProvider]);
        _sut = new PlayerService(codecManager, _factory);
    }

    [Fact]
    public void Initialize_CreatesPlayer()
    {
        _sut.Initialize();
        _sut.Player.ShouldNotBeNull();
        _factory.Received(1).CreatePlayer();
    }

    [Fact]
    public void Initialize_DoubleInit_IsNoOp()
    {
        _sut.Initialize();
        _sut.Initialize();

        _factory.Received(1).CreatePlayer(); // only once
    }

    [Fact]
    public void Initialize_FactoryThrows_PropagatesException()
    {
        _factory.CreatePlayer().Returns(x => throw new System.InvalidOperationException("fail"));

        Should.Throw<System.InvalidOperationException>(() => _sut.Initialize());
    }

    [Fact]
    public void Initialize_SubscribesToPlayerError()
    {
        _sut.Initialize();
        var wasCalled = false;
        _sut.Error += (_, _) => wasCalled = true;

        _mockPlayer.Error += Raise.Event<System.EventHandler<string>>(_mockPlayer, "test error");

        wasCalled.ShouldBeTrue();
    }

    [Fact]
    public void Dispose_UnsubscribesFromPlayer()
    {
        _sut.Initialize();
        _sut.Dispose();

        // After dispose, raising error should not propagate
        var wasCalled = false;
        _sut.Error += (_, _) => wasCalled = true;
        _mockPlayer.Error += Raise.Event<System.EventHandler<string>>(_mockPlayer, "test");

        wasCalled.ShouldBeFalse();
    }

    [Fact]
    public void Dispose_CallsPlayerStop()
    {
        _sut.Initialize();
        _sut.Dispose();

        _mockPlayer.Received(1).Stop();
    }

    [Fact]
    public void Dispose_DoubleDispose_DoesNotThrow()
    {
        _sut.Initialize();
        _sut.Dispose();
        Should.NotThrow(() => _sut.Dispose());
    }

    [Fact]
    public void Player_BeforeInit_ReturnsNull()
    {
        _sut.Player.ShouldBeNull();
    }
}
