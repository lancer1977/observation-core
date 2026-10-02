using BenchmarkDotNet.Attributes;
using Observation.Core;

namespace Observation.Core.Benchmarks;

[MemoryDiagnoser]
public class ScopeBenchmarks
{
    private ObservationScope scope = null!;
    private ObservationScope prefix = null!;

    [Params(4, 16)]
    public int Depth { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var segments = Enumerable.Range(0, Depth).Select(index => $"segment-{index}").ToArray();
        scope = ObservationScope.Of(segments);
        prefix = ObservationScope.Of(segments[..(Depth / 2)]);
    }

    [Benchmark]
    public ObservationScope Append() => scope.Append("next");

    [Benchmark]
    public bool Compare() => scope.CompareTo(prefix) > 0;
}
