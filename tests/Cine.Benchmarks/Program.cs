using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace Cine.Benchmarks;

/// <summary>
/// Uses InProcessEmitToolchain to avoid BDN generating a temporary project
/// targeting "net10.0" (without "-windows") which can't resolve our
/// Windows-specific dependencies.
/// </summary>
public class BenchConfig : ManualConfig
{
    public BenchConfig()
    {
        AddJob(Job.Default
            .WithToolchain(InProcessEmitToolchain.Instance)
            .WithId("InProcess"));
        AddLogger(ConsoleLogger.Default);
    }
}

public static class Program
{
    public static void Main(string[] args)
    {
        var config = new BenchConfig();
        BenchmarkRunner.Run<PlaybackStateManagerBenchmarks>(config);
        BenchmarkRunner.Run<PlaylistCoordinatorBenchmarks>(config);
        BenchmarkRunner.Run<AudioManagerBenchmarks>(config);
    }
}
