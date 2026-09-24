using Xunit;

namespace Observation.Core.Tests.Caching;

public sealed class ObservationCacheTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

    [Fact]
    public async Task HitsPreserveValueTimestampAndEvidenceId()
    {
        var cache = new ObservationCache<string, string>(clock: () => Now);
        var id = Guid.NewGuid();
        var first = await cache.GetOrCreateAsync(Request("a"), (_, _) => Task.FromResult<ObservationCacheValue<string>?>(new("value", Now, id)));
        var hit = await cache.GetOrCreateAsync(Request("a"), (_, _) => throw new Xunit.Sdk.XunitException("must hit"));
        Assert.Equal(ObservationCacheDisposition.Miss, first.Evidence.Disposition);
        Assert.Equal(ObservationCacheDisposition.Hit, hit.Evidence.Disposition);
        Assert.Equal("value", hit.Value);
        Assert.Equal(id, hit.Evidence.EvidenceId);
    }

    [Fact]
    public async Task CachesObservationWithoutRewritingItsEvidence()
    {
        var observation = new Observation<string>(Guid.NewGuid(), ObservationScope.Of("scope"), "subject", "predicate", "value", "source", "kind", 0.9, Now);
        var cache = new ObservationCache<string, Observation<string>>(clock: () => Now);
        var result = await cache.GetOrCreateAsync(Request("observation"), (_, _) => Task.FromResult<ObservationCacheValue<Observation<string>>?>(new(observation, observation.ObservedAt, observation.Id)));
        var hit = await cache.GetOrCreateAsync(Request("observation"), (_, _) => throw new Xunit.Sdk.XunitException("must hit"));
        Assert.Same(observation, result.Value);
        Assert.Same(observation, hit.Value);
        Assert.Equal(observation.Id, hit.Evidence.EvidenceId);
    }

    [Fact]
    public async Task ExactBoundaryFutureAndMissingEvidenceAreUnknown()
    {
        var time = Now;
        var cache = new ObservationCache<string, string>(clock: () => time);
        var boundary = await cache.GetOrCreateAsync(Request("boundary") with { MaxAge = TimeSpan.FromMinutes(1) }, (_, _) => Task.FromResult<ObservationCacheValue<string>?>(new("old", time.AddMinutes(-1))));
        var future = await cache.GetOrCreateAsync(Request("future"), (_, _) => Task.FromResult<ObservationCacheValue<string>?>(new("future", time.AddSeconds(1))));
        var missing = await cache.GetOrCreateAsync(Request("missing") with { Input = null! }, (_, _) => throw new Xunit.Sdk.XunitException("must not parse"));
        Assert.Null(boundary.Value);
        Assert.Null(future.Value);
        Assert.Null(missing.Value);
        Assert.Equal(ObservationCacheDisposition.Unknown, boundary.Evidence.Disposition);
    }

    [Fact]
    public async Task FingerprintBacktrackCannotPublishLateParse()
    {
        var cache = new ObservationCache<string, string>(clock: () => Now);
        var gate = new TaskCompletionSource<ObservationCacheValue<string>?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = cache.GetOrCreateAsync(Request("old"), (_, _) => gate.Task);
        await WaitUntilAsync(() => cache.PendingCount == 1);
        var newer = await cache.GetOrCreateAsync(Request("new") with { Fingerprint = "new" }, (_, _) => Task.FromResult<ObservationCacheValue<string>?>(new("new", Now)));
        gate.SetResult(new("old", Now));
        var oldResult = await old;
        Assert.Equal("new", newer.Value);
        Assert.Equal(ObservationCacheDisposition.Invalidated, oldResult.Evidence.Disposition);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public async Task ScopeInvalidationUsesPrefixSegmentsAndCancelsPending()
    {
        var cache = new ObservationCache<string, string>(clock: () => Now);
        await cache.GetOrCreateAsync(Request("child") with { Scope = ObservationScope.Of("a", "b") }, (_, _) => Task.FromResult<ObservationCacheValue<string>?>(new("child", Now)));
        await cache.GetOrCreateAsync(Request("sibling") with { Scope = ObservationScope.Of("ab") }, (_, _) => Task.FromResult<ObservationCacheValue<string>?>(new("sibling", Now)));
        cache.InvalidateScope(ObservationScope.Of("a"));
        Assert.Equal(1, cache.Count);
        var sibling = await cache.GetOrCreateAsync(Request("sibling") with { Scope = ObservationScope.Of("ab") }, (_, _) => throw new Xunit.Sdk.XunitException("must remain"));
        Assert.Equal("sibling", sibling.Value);
    }

    [Fact]
    public async Task TagInvalidationLeavesUnrelatedEntryUntouched()
    {
        var cache = new ObservationCache<string, string>(clock: () => Now);
        await cache.GetOrCreateAsync(Request("tagged") with { Tags = new HashSet<string>(["changed"]) }, (_, _) => Task.FromResult<ObservationCacheValue<string>?>(new("tagged", Now)));
        await cache.GetOrCreateAsync(Request("other") with { Fingerprint = "other", Tags = new HashSet<string>(["other"]) }, (_, _) => Task.FromResult<ObservationCacheValue<string>?>(new("other", Now)));
        cache.InvalidateTag("changed");
        Assert.Equal(1, cache.Count);
        var other = await cache.GetOrCreateAsync(Request("other") with { Fingerprint = "other", Tags = new HashSet<string>(["other"]) }, (_, _) => throw new Xunit.Sdk.XunitException("must remain"));
        Assert.Equal("other", other.Value);
    }

    [Fact]
    public async Task CallerCancellationDoesNotCacheUncooperativeLateResult()
    {
        var cache = new ObservationCache<string, string>(clock: () => Now);
        var gate = new TaskCompletionSource<ObservationCacheValue<string>?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        var parsing = cache.GetOrCreateAsync(Request("a"), (_, _) => gate.Task, cancellation.Token);
        await WaitUntilAsync(() => cache.PendingCount == 1);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => parsing);
        gate.SetResult(new("late", Now));
        await WaitUntilAsync(() => cache.PendingCount == 0);
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public async Task ParserFailureAndInvalidationReleaseBookkeeping()
    {
        var cache = new ObservationCache<string, string>(clock: () => Now);
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetOrCreateAsync(Request("fail"), (_, _) => throw new InvalidOperationException("parse")));
        Assert.Equal(0, cache.PendingCount);
        Assert.Equal(0, cache.TrackedScopeCount);
        var gate = new TaskCompletionSource<ObservationCacheValue<string>?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var parsing = cache.GetOrCreateAsync(Request("pending") with { Tags = new HashSet<string>(["x"]) }, (_, _) => gate.Task);
        await WaitUntilAsync(() => cache.PendingCount == 1);
        cache.InvalidateTag("x");
        gate.SetResult(new("late", Now));
        var result = await parsing;
        Assert.Equal(ObservationCacheDisposition.Invalidated, result.Evidence.Disposition);
        Assert.Equal(0, cache.PendingCount);
        Assert.Equal(0, cache.TrackedScopeCount);
    }

    [Fact]
    public async Task FifoCapacityBoundsEntriesAndMetadata()
    {
        var cache = new ObservationCache<string, string>(2, () => Now);
        for (var index = 0; index < 10; index++)
        {
            await cache.GetOrCreateAsync(Request(index.ToString()), (_, _) => Task.FromResult<ObservationCacheValue<string>?>(new(index.ToString(), Now)));
            Assert.InRange(cache.Count, 0, 2);
            Assert.InRange(cache.TrackedScopeCount, 0, 2);
        }

        var calls = 0;
        await cache.GetOrCreateAsync(Request("0"), (_, _) => { calls++; return Task.FromResult<ObservationCacheValue<string>?>(new("again", Now)); });
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ConcurrentSameKeyCallsRemainBoundedAndSafe()
    {
        var cache = new ObservationCache<string, string>(2, () => Now);
        var gate = new TaskCompletionSource<ObservationCacheValue<string>?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondGate = new TaskCompletionSource<ObservationCacheValue<string>?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = cache.GetOrCreateAsync(Request("same"), (_, _) => gate.Task);
        var second = cache.GetOrCreateAsync(Request("same"), (_, _) => secondGate.Task);
        await WaitUntilAsync(() => cache.PendingCount == 2);
        gate.SetResult(new("first", Now));
        secondGate.SetResult(new("second", Now));
        var results = await Task.WhenAll(first, second);
        Assert.All(results, result => Assert.Equal(ObservationCacheDisposition.Miss, result.Evidence.Disposition));
        Assert.InRange(cache.Count, 1, 2);
    }

    [Fact]
    public async Task ParserVersionChangeInvalidatesEarlierVersionAndBacktrackMisses()
    {
        var cache = new ObservationCache<string, string>(clock: () => Now);
        var calls = 0;
        Task<ObservationCacheValue<string>?> Parse(string input, CancellationToken _)
        {
            calls++;
            return Task.FromResult<ObservationCacheValue<string>?>(new(input, Now));
        }

        await cache.GetOrCreateAsync(Request("a"), Parse);
        await cache.GetOrCreateAsync(Request("a") with { ParserVersion = "v2" }, Parse);
        await cache.GetOrCreateAsync(Request("a"), Parse);

        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task ParserVersionChangeCancelsLateParseFromEarlierVersion()
    {
        var cache = new ObservationCache<string, string>(clock: () => Now);
        var gate = new TaskCompletionSource<ObservationCacheValue<string>?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = cache.GetOrCreateAsync(Request("a"), (_, _) => gate.Task);
        await WaitUntilAsync(() => cache.PendingCount == 1);

        var newer = await cache.GetOrCreateAsync(Request("a") with { ParserVersion = "v2" }, (_, _) => Task.FromResult<ObservationCacheValue<string>?>(new("new", Now)));
        gate.SetResult(new("old", Now));
        var oldResult = await old;

        Assert.Equal("new", newer.Value);
        Assert.Equal(ObservationCacheDisposition.Invalidated, oldResult.Evidence.Disposition);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public async Task MissingInputInvalidatesEarlierReuseForThatScope()
    {
        var cache = new ObservationCache<string, string>(clock: () => Now);
        var calls = 0;
        Task<ObservationCacheValue<string>?> Parse(string input, CancellationToken _)
        {
            calls++;
            return Task.FromResult<ObservationCacheValue<string>?>(new(input, Now));
        }

        await cache.GetOrCreateAsync(Request("a"), Parse);
        var missing = await cache.GetOrCreateAsync(Request("a") with { Input = null! }, Parse);
        await cache.GetOrCreateAsync(Request("a"), Parse);

        Assert.Null(missing.Value);
        Assert.Equal(ObservationCacheDisposition.Unknown, missing.Evidence.Disposition);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void InvalidArgumentsAreRejectedAndTagsAreCopied()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ObservationCache<string, string>(0));
        Assert.Throws<ArgumentNullException>(() => new ObservationCacheRequest<string>("x", null!, "f", "v", TimeSpan.Zero));
        Assert.Throws<ArgumentException>(() => new ObservationCacheRequest<string>("x", ObservationScope.Of("s"), " ", "v", TimeSpan.Zero));
        Assert.Throws<ArgumentException>(() => new ObservationCacheRequest<string>("x", ObservationScope.Of("s"), "f", " ", TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ObservationCacheRequest<string>("x", ObservationScope.Of("s"), "f", "v", TimeSpan.FromSeconds(-1)));
        var source = new HashSet<string>(["tag"]);
        var request = new ObservationCacheRequest<string>("x", ObservationScope.Of("s"), "f", "v", TimeSpan.Zero, source);
        source.Clear();
        Assert.Contains("tag", request.Tags);
    }

    private static ObservationCacheRequest<string> Request(string input) => new(input, ObservationScope.Of("root", "slot"), input, "v1", TimeSpan.FromMinutes(1), new HashSet<string>(["default"]));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Yield();
        }

        Assert.True(condition());
    }
}
