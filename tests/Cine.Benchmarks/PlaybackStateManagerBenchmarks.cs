using BenchmarkDotNet.Attributes;
using Cine.Avalonia.Managers;
using Cine.Media.Events;
using Cine.Media.Interfaces;
using NSubstitute;

namespace Cine.Benchmarks;

[MemoryDiagnoser]
public class PlaybackStateManagerBenchmarks
{
    private IMediaPlayer _player = null!;
    private PlaybackStateManager _sut = null!;

    [IterationSetup]
    public void Setup()
    {
        _player = Substitute.For<IMediaPlayer>();
        _sut = new PlaybackStateManager(_player);
    }

    [Benchmark(Description = "1000 PositionChanged events")]
    public void ThousandPositionEvents()
    {
        var args = new PositionChangedEventArgs(
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(100));

        for (int i = 0; i < 1000; i++)
        {
            _player.PositionChanged += Raise.Event<EventHandler<PositionChangedEventArgs>>(args);
        }
    }

    [Benchmark(Description = "1000 VolumeChanged events")]
    public void ThousandVolumeEvents()
    {
        for (int i = 0; i < 1000; i++)
        {
            _player.VolumeChanged += Raise.Event<EventHandler<VolumeChangedEventArgs>>(
                new VolumeChangedEventArgs(i % 100));
        }
    }

    [Benchmark(Description = "1000 PlaybackStateChanged transitions")]
    public void ThousandStateTransitions()
    {
        for (int i = 0; i < 1000; i++)
        {
            _player.PlaybackStateChangedEvent +=
                Raise.Event<EventHandler<PlaybackStateChangedEventArgs>>(
                    new PlaybackStateChangedEventArgs(isPaused: i % 2 == 0));
        }
    }

    [Benchmark(Description = "1000 Refresh() calls")]
    public void ThousandRefreshCalls()
    {
        for (int i = 0; i < 1000; i++)
        {
            _sut.Refresh();
        }
    }
}
