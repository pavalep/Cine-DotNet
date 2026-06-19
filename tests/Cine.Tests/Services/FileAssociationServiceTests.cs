using Cine.Avalonia.Services;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cine.Tests.Services;

public class FileAssociationServiceTests
{
    private readonly IRegistryService _mockRegistry;
    private const string TestExePath = @"C:\Program Files\Cine\Cine.exe";

    public FileAssociationServiceTests()
    {
        _mockRegistry = Substitute.For<IRegistryService>();
    }

    [Fact]
    public void Register_SetsExpectedRegistryKeys()
    {
        var svc = new FileAssociationService(_mockRegistry, TestExePath);

        svc.Register();

        // Should set command path for video formats
        _mockRegistry.Received().SetValue(
            "CineMediaPlayer\\shell\\open\\command", "",
            $"\"{TestExePath}\" \"%1\"");

        // Should set friendly name
        _mockRegistry.Received().SetValue("CineMediaPlayer", "", Arg.Is<string>(s => s.Contains("Cine Media Player")));

        // Should set progid for at least one video format
        _mockRegistry.Received().SetBinaryValue(
            ".mp4\\OpenWithProgids", "CineMediaPlayer", Arg.Any<byte[]>());

        // Should set progid for at least one audio format
        _mockRegistry.Received().SetBinaryValue(
            ".mp3\\OpenWithProgids", "CineMediaPlayer", Arg.Any<byte[]>());

        // Should set progid for at least one subtitle format
        _mockRegistry.Received().SetBinaryValue(
            ".srt\\OpenWithProgids", "CineMediaPlayer.sub", Arg.Any<byte[]>());
    }

    [Fact]
    public void Unregister_RemovesRegistryKeys()
    {
        var svc = new FileAssociationService(_mockRegistry, TestExePath);

        svc.Unregister();

        // Should delete progid values for video, audio, and subtitle formats
        _mockRegistry.Received().DeleteValue(".mp4\\OpenWithProgids", "CineMediaPlayer");
        _mockRegistry.Received().DeleteValue(".mp3\\OpenWithProgids", "CineMediaPlayer");
        _mockRegistry.Received().DeleteValue(".srt\\OpenWithProgids", "CineMediaPlayer.sub");
    }

    [Fact]
    public void IsRegistered_ReturnsTrue_WhenKeyExists()
    {
        _mockRegistry.GetValue(".mp4\\OpenWithProgids", "CineMediaPlayer")
            .Returns(new byte[0]);

        var svc = new FileAssociationService(_mockRegistry, TestExePath);
        svc.IsRegistered(".mp4").ShouldBeTrue();
    }

    [Fact]
    public void IsRegistered_ReturnsFalse_WhenKeyMissing()
    {
        _mockRegistry.GetValue(".mp4\\OpenWithProgids", "CineMediaPlayer")
            .Returns((object?)null);

        var svc = new FileAssociationService(_mockRegistry, TestExePath);
        svc.IsRegistered(".mp4").ShouldBeFalse();
    }

    [Fact]
    public void Register_FormatFailure_DoesNotBlockOtherFormats()
    {
        var callCount = 0;
        _mockRegistry.When(x => x.SetValue(
                "CineMediaPlayer\\shell\\open\\command", "",
                Arg.Any<string>()))
            .Do(_ =>
            {
                callCount++;
                if (callCount == 1)
                    throw new InvalidOperationException("First format fails");
            });

        var svc = new FileAssociationService(_mockRegistry, TestExePath);

        // Should not throw — per-format try-catch isolates failures
        Should.NotThrow(() => svc.Register());

        // Should have attempted all formats (at minimum the ones after the failing one)
        callCount.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void RegisterOnStartup_WithInvalidExePath_SkipsRegistration()
    {
        var svc = new FileAssociationService(_mockRegistry, "not-an-exe-path");

        svc.RegisterOnStartup();

        // Should not call Register (no registry writes)
        _mockRegistry.DidNotReceiveWithAnyArgs().SetValue(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object?>());
    }

    [Fact]
    public void RegisterOnStartup_WithValidExePath_QueuesRegistration()
    {
        var svc = new FileAssociationService(_mockRegistry, TestExePath);

        svc.RegisterOnStartup();

        // RegisterOnStartup queues work on thread pool — give it time
        Thread.Sleep(100);

        // Should have set at least the command path
        _mockRegistry.Received().SetValue(
            "CineMediaPlayer\\shell\\open\\command", "",
            $"\"{TestExePath}\" \"%1\"");
    }
}
