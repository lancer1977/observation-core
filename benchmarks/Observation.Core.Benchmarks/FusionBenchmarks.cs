using BenchmarkDotNet.Attributes;
using Observation.Core;

namespace Observation.Core.Benchmarks;

[MemoryDiagnoser]
public class FusionBenchmarks
{
    private static readonly DateTimeOffset ObservedAt = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
    private Observation<string>[] observations = [];
    private ObservationFusion<string> fusion = new();

    [Params(10, 100, 1000)]
    public int ObservationCount { get; set; }

    [Params(FusionKind.Agree, FusionKind.Conflict)]
    public FusionKind Kind { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        observations = Enumerable.Range(0, ObservationCount)
            .Select(index => new Observation<string>(
                Guid.Parse($"00000000-0000-0000-0000-{index + 1:D12}"),
                ObservationScope.Of("root", "slot"),
                "subject",
                "predicate",
                Kind == FusionKind.Agree || index % 2 == 0 ? "open" : "closed",
                $"source-{index % 4}",
                "benchmark",
                Kind == FusionKind.Agree ? 0.8 : 0.6 + (index % 4) * 0.05,
                ObservedAt))
            .ToArray();
        fusion = new ObservationFusion<string>();
    }

    [Benchmark]
    public IReadOnlyList<FusionResult<string>> Resolve() => fusion.Resolve(observations);
}

public enum FusionKind
{
    Agree,
    Conflict
}
