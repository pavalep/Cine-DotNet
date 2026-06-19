using BenchmarkDotNet.Attributes;
using Cine.Avalonia.Services;

namespace Cine.Benchmarks;

[MemoryDiagnoser]
public class PlaylistCoordinatorBenchmarks
{
    private PlaylistCoordinator _small = null!;
    private PlaylistCoordinator _medium = null!;
    private PlaylistCoordinator _large = null!;

    [IterationSetup]
    public void Setup()
    {
        _small = new PlaylistCoordinator();
        _medium = new PlaylistCoordinator();
        _large = new PlaylistCoordinator();

        for (int i = 0; i < 100; i++)
            _small.Add($@"C:\test\movie_{i:D3}.mp4");

        for (int i = 0; i < 1000; i++)
            _medium.Add($@"C:\test\movie_{i:D3}.mp4");

        for (int i = 0; i < 10000; i++)
            _large.Add($@"C:\test\movie_{i:D3}.mp4");
    }

    // ── Sort ─────────────────────────────────────────────────────

    [Benchmark(Description = "Sort 100 items")]
    public void Sort100() => _small.SortByTitle();

    [Benchmark(Description = "Sort 1000 items")]
    public void Sort1000() => _medium.SortByTitle();

    [Benchmark(Description = "Sort 10000 items")]
    public void Sort10000() => _large.SortByTitle();

    // ── Shuffle ──────────────────────────────────────────────────

    [Benchmark(Description = "Shuffle 100 items")]
    public void Shuffle100() => _small.Shuffle();

    [Benchmark(Description = "Shuffle 10000 items")]
    public void Shuffle10000() => _large.Shuffle();

    // ── Navigation ──────────────────────────────────────────────

    [Benchmark(Description = "GetNextIndex 1000 times (100 items, no wrap)")]
    public void NavigationThousand()
    {
        _small.CurrentIndex = 0;
        for (int i = 0; i < 1000; i++)
            _small.GetNextIndex();
    }

    // ── Add/Remove ───────────────────────────────────────────────

    [Benchmark(Description = "Add 1000 items")]
    public void AddThousand()
    {
        var pl = new PlaylistCoordinator();
        for (int i = 0; i < 1000; i++)
            pl.Add($@"C:\test\movie_{i:D3}.mp4");
    }

    [Benchmark(Description = "Remove 1000 items (from end)")]
    public void RemoveThousand()
    {
        for (int i = _medium.Count - 1; i >= 0; i--)
            _medium.RemoveAt(i);
    }
}
