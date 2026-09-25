using Xunit;

namespace Observation.Core.Tests.Properties;

public sealed class CacheInvariantTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SeededCacheInvariantsHoldForAtLeastTwoHundredSeeds()
    {
        for (var seed = 0; seed < 256; seed++)
        {
            try
            {
                await CheckAsync(seed);
            }
            catch (Exception exception)
            {
                throw new Xunit.Sdk.XunitException($"Cache invariant failed for seed {seed}.", exception);
            }
        }
    }

    [Fact]
    public void NegativeControl_OffByOneCapacityIsDetected()
    {
        var exception = Assert.Throws<Xunit.Sdk.XunitException>(() => AssertCapacity(31, 2, 3));
        Assert.Contains("seed 31", exception.Message);
    }

    private static async Task CheckAsync(int seed)
    {
        var random = new Random(seed);
        var capacity = random.Next(1, 6);
        var cache = new ObservationCache<string, string>(capacity, () => Now);
        for (var index = 0; index < 24; index++)
        {
            var scope = ObservationScope.Of("root", $"slot-{random.Next(0, 10)}");
            var request = Request($"input-{index}", scope, $"fingerprint-{index % 3}", $"parser-{index % 2}");
            await cache.GetOrCreateAsync(request, (input, _) =>
                Task.FromResult<ObservationCacheValue<string>?>(new(input, Now)));
            AssertCapacity(seed, capacity, cache.Count);
            Assert.InRange(cache.PendingCount, 0, capacity);
            Assert.InRange(cache.TrackedScopeCount, 0, capacity);
        }

        var lateCache = new ObservationCache<string, string>(Math.Max(2, capacity), () => Now);
        var gate = new TaskCompletionSource<ObservationCacheValue<string>?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldRequest = Request("old", ObservationScope.Of("late", $"seed-{seed}"), "old-fingerprint", "v1");
        var old = lateCache.GetOrCreateAsync(oldRequest, (_, _) => gate.Task);
        await WaitUntilAsync(() => lateCache.PendingCount == 1);
        var newer = await lateCache.GetOrCreateAsync(oldRequest with { Input = "new", Fingerprint = "new-fingerprint" },
            (_, _) => Task.FromResult<ObservationCacheValue<string>?>(new("new", Now)));
        gate.SetResult(new("old", Now));
        var oldResult = await old;
        Assert.Equal("new", newer.Value);
        Assert.Equal(ObservationCacheDisposition.Invalidated, oldResult.Evidence.Disposition);
        Assert.Equal(1, lateCache.Count);
        Assert.Equal(0, lateCache.PendingCount);

        using var cancellation = new CancellationTokenSource();
        var cancelGate = new TaskCompletionSource<ObservationCacheValue<string>?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = lateCache.GetOrCreateAsync(Request("cancel", ObservationScope.Of("cancel", $"seed-{seed}"), "cancel", "v1"),
            (_, _) => cancelGate.Task, cancellation.Token);
        await WaitUntilAsync(() => lateCache.PendingCount == 1);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        cancelGate.SetResult(new("late", Now));
        await WaitUntilAsync(() => lateCache.PendingCount == 0);
        Assert.Equal(1, lateCache.Count);
    }

    private static void AssertCapacity(int seed, int capacity, int count)
    {
        try
        {
            Assert.InRange(count, 0, capacity);
        }
        catch (Exception exception)
        {
            throw new Xunit.Sdk.XunitException($"seed {seed}: cache exceeded capacity {capacity} with {count} entries.", exception);
        }
    }

    private static ObservationCacheRequest<string> Request(string input, ObservationScope scope, string fingerprint, string parserVersion) =>
        new(input, scope, fingerprint, parserVersion, TimeSpan.FromMinutes(1), new HashSet<string>(["property"]));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Yield();
        }

        Assert.True(condition());
    }
}
