using BenchmarkDotNet.Attributes;
using Cine.Avalonia.Managers;
using Cine.Media.Interfaces;
using NSubstitute;

namespace Cine.Benchmarks;

[MemoryDiagnoser]
public class AudioManagerBenchmarks
{
    private IMediaPlayer _player = null!;
    private AudioManager _sut = null!;

    [IterationSetup]
    public void Setup()
    {
        _player = Substitute.For<IMediaPlayer>();
        _sut = new AudioManager(_player);
    }

    [Benchmark(Description = "Set 10 equalizer bands")]
    public void SetTenEqualizerBands()
    {
        for (int i = 0; i < 10; i++)
            _sut.SetEqualizerBand(i, i * 2 - 10);
    }

    [Benchmark(Description = "Apply Rock preset")]
    public void ApplyRockPreset() => _sut.ApplyEqualizerPreset("Rock");

    [Benchmark(Description = "Toggle normalization 100 times")]
    public void ToggleNormalization100()
    {
        for (int i = 0; i < 100; i++)
            _sut.ToggleAudioNormalization();
    }

    [Benchmark(Description = "Volume value changes 1000 times")]
    public void VolumeChanges1000()
    {
        for (int i = 0; i < 1000; i++)
            _sut.VolumeValue = i % 100;
    }

    [Benchmark(Description = "Increase/Decrease volume 500 times each")]
    public void VolumeAdjustments1000()
    {
        _sut.VolumeValue = 50;
        for (int i = 0; i < 500; i++)
        {
            _sut.IncreaseVolume();
            _sut.DecreaseVolume();
        }
    }
}
