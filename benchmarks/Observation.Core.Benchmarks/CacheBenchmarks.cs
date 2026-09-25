using BenchmarkDotNet.Attributes;
using Observation.Core;

namespace Observation.Core.Benchmarks;

[MemoryDiagnoser]
public class CacheBenchmarks
{
    private static readonly DateTimeOffset ObservedAt = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
    private ObservationCache<string, string> cache = null!;
    private ObservationCacheRequest<string> request = null!;
    private int missFingerprint;

    [GlobalSetup]
    public async Task Setup()
    {
        cache = new ObservationCache<string, string>(capacity: 2048, clock: () => ObservedAt);
        request = new ObservationCacheRequest<string>(
            "input",
            ObservationScope.Of("root", "slot"),
            "fingerprint",
            "v1",
            TimeSpan.FromMinutes(1),
            new HashSet<string>(["benchmark"]));
        await cache.GetOrCreateAsync(request, Parse);
    }

    [Benchmark]
    public Task<ObservationCacheResult<string>> Hit() => cache.GetOrCreateAsync(request, Parse);

    [Benchmark]
    public Task<ObservationCacheResult<string>> Miss()
    {
        var current = Interlocked.Increment(ref missFingerprint);
        return cache.GetOrCreateAsync(request with { Fingerprint = $"miss-{current}" }, Parse);
    }

    [Benchmark]
    public void Invalidate() => cache.InvalidateTag("benchmark");

    [IterationSetup(Target = nameof(Invalidate))]
    public void SetupInvalidation()
    {
        var current = Interlocked.Increment(ref missFingerprint);
        cache.GetOrCreateAsync(request with { Fingerprint = $"invalidate-{current}" }, Parse)
            .GetAwaiter()
            .GetResult();
    }

    private static Task<ObservationCacheValue<string>?> Parse(string input, CancellationToken _) =>
        Task.FromResult<ObservationCacheValue<string>?>(new(input, ObservedAt));
}
