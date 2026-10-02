using Observation.Core;
using Xunit;

namespace Observation.Core.Tests.Compatibility.Caching;

public sealed class ObservationCacheCompatibilityTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Compat_ReusesValueAndEvidenceIdWithoutRefreshingObservedAt()
    {
        // AGH source test: ReusesActualEnvelopeAndNotebookEvidenceWithoutRefreshingItsAge
        var cache = new ObservationCache<string, Observation<string>>(clock: () => Now.AddSeconds(1));
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var observation = new Observation<string>(id, Scope("ctx-a", "run-1", "zone-1", "slot-x"), "subject", "predicate", "value", "source", "kind", .9, Now);
        var evidence = new ObservationCacheValue<Observation<string>>(observation, observation.ObservedAt, observation.Id);
        var calls = 0;

        await cache.GetOrCreateAsync(Request("same"), (_, _) =>
        {
            calls++;
            return Task.FromResult<ObservationCacheValue<Observation<string>>?>(evidence);
        });
        var hit = await cache.GetOrCreateAsync(Request("same"), (_, _) =>
        {
            calls++;
            return Task.FromResult<ObservationCacheValue<Observation<string>>?>(null);
        });

        Assert.Equal(1, calls);
        Assert.Same(observation, hit.Value);
        Assert.Equal(observation.ObservedAt, hit.Value!.ObservedAt);
        Assert.Equal(id, hit.Evidence.EvidenceId);
        Assert.Equal(ObservationCacheDisposition.Hit, hit.Evidence.Disposition);
    }

    [Fact]
    public async Task Compat_MissingStaleFutureAndExactExpiryAreUnknownWithoutOldValue()
    {
        // AGH source test: MissingStaleFutureAndExactExpiryAreUnknownWithoutReturningOldValue
        var time = Now;
        var cache = new ObservationCache<string, string>(clock: () => time);
        var stale = await cache.GetOrCreateAsync(Request("stale"), (_, _) =>
            Task.FromResult<ObservationCacheValue<string>?>(new("old", Now.AddMinutes(-1))));
        var future = await cache.GetOrCreateAsync(Request("future"), (_, _) =>
            Task.FromResult<ObservationCacheValue<string>?>(new("future", Now.AddSeconds(1))));
        var missing = await cache.GetOrCreateAsync(Request("missing") with { Input = null! }, (_, _) =>
            throw new Xunit.Sdk.XunitException("parser must not run"));

        Assert.Null(stale.Value);
        Assert.Null(future.Value);
        Assert.Null(missing.Value);
        Assert.Equal(ObservationCacheDisposition.Unknown, stale.Evidence.Disposition);
        Assert.Equal(ObservationCacheDisposition.Unknown, future.Evidence.Disposition);
        Assert.Equal(ObservationCacheDisposition.Unknown, missing.Evidence.Disposition);

        await cache.GetOrCreateAsync(Request("live"), (_, _) =>
            Task.FromResult<ObservationCacheValue<string>?>(new("live", Now)));
        time = Now.AddMinutes(1);
        var expired = await cache.GetOrCreateAsync(Request("live"), (_, _) =>
            Task.FromResult<ObservationCacheValue<string>?>(new("old", time.AddMinutes(-1))));
        Assert.Null(expired.Value);
        Assert.Equal(ObservationCacheDisposition.Unknown, expired.Evidence.Disposition);
    }

    [Fact]
    public async Task Compat_TagInvalidationCancelsPendingAndRejectsLateValue()
    {
        // AGH source test: RelevantInvalidationCancelsPendingAndCannotPublishLateValue
        var cache = new ObservationCache<string, string>(clock: () => Now);
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource<ObservationCacheValue<string>?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var parsing = cache.GetOrCreateAsync(Request("pending") with { Tags = new HashSet<string>(["changed"]) }, async (_, _) =>
        {
            started.SetResult(true);
            return await gate.Task;
        });

        await started.Task;
        cache.InvalidateTag("changed");
        gate.SetResult(new("late", Now));
        var result = await parsing;

        Assert.Null(result.Value);
        Assert.Equal(ObservationCacheDisposition.Invalidated, result.Evidence.Disposition);
        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.PendingCount);
    }

    [Fact]
    public async Task Compat_PreCancelledRequestSkipsParserAndVersionBacktrackReparses()
    {
        // AGH source test: PreCancelledRequestDoesNotInvokeParserAndParserVersionBacktrackMisses
        var cache = new ObservationCache<string, string>(clock: () => Now);
        var calls = 0;
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => cache.GetOrCreateAsync(Request("a"), (_, _) =>
        {
            calls++;
            return Task.FromResult<ObservationCacheValue<string>?>(new("bad", Now));
        }, cancelled.Token));

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
    public async Task Compat_ScopePrefixInvalidationExpressesContextTransitionPolicy()
    {
        // AGH source test: SceneAndSessionTransitionsInvalidateEarlierEvidence
        // The automatic context policy is intentionally not upstream behavior; this proves its primitive equivalent.
        var cache = new ObservationCache<string, string>(clock: () => Now);
        var calls = 0;
        Task<ObservationCacheValue<string>?> Parse(string input, CancellationToken _)
        {
            calls++;
            return Task.FromResult<ObservationCacheValue<string>?>(new(input, Now));
        }

        var parent = Scope("ctx-a", "run-1");
        var first = Request("a") with { Scope = parent.Append("zone-1").Append("slot-x") };
        await cache.GetOrCreateAsync(first, Parse);
        cache.InvalidateScope(parent);
        await cache.GetOrCreateAsync(first, Parse);

        Assert.Equal(2, calls);

        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource<ObservationCacheValue<string>?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = cache.GetOrCreateAsync(first with { Scope = parent.Append("zone-2").Append("slot-x") }, async (_, _) =>
        {
            started.SetResult(true);
            return await gate.Task;
        });
        await started.Task;
        cache.InvalidateScope(parent);
        gate.SetResult(new("late", Now));
        var invalidated = await pending;

        Assert.Equal(ObservationCacheDisposition.Invalidated, invalidated.Evidence.Disposition);
        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.PendingCount);
    }

    [Fact]
    public async Task Compat_MultipleSlotsRemainWithinFifoAndBookkeepingBounds()
    {
        // AGH source test: SlotsHaveBoundedFifoEntriesAndBookkeeping
        var cache = new ObservationCache<string, string>(2, () => Now);
        var calls = 0;
        Task<ObservationCacheValue<string>?> Parse(string input, CancellationToken _)
        {
            calls++;
            return Task.FromResult<ObservationCacheValue<string>?>(new(input, Now));
        }

        for (var index = 0; index < 100; index++)
        {
            await cache.GetOrCreateAsync(Request("a") with { Scope = Scope("ctx-a", "run-1", "zone-1", $"slot-{index}") }, Parse);
            Assert.InRange(cache.Count, 0, 2);
            Assert.InRange(cache.TrackedScopeCount, 0, 2);
            Assert.Equal(0, cache.PendingCount);
        }

        await cache.GetOrCreateAsync(Request("a") with { Scope = Scope("ctx-a", "run-1", "zone-1", "slot-99") }, Parse);
        Assert.Equal(100, calls);
        await cache.GetOrCreateAsync(Request("a") with { Scope = Scope("ctx-a", "run-1", "zone-1", "slot-0") }, Parse);
        Assert.Equal(101, calls);
    }

    [Fact]
    public async Task Compat_CallerCancellationBoundsUncooperativeParserAndRejectsLateResult()
    {
        // AGH source test: CallerCancellationBoundsUncooperativeParserAndNeverCachesLateResult
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
        var fresh = await cache.GetOrCreateAsync(Request("a"), (_, _) =>
            Task.FromResult<ObservationCacheValue<string>?>(new("fresh", Now)));
        Assert.Equal("fresh", fresh.Value);
    }

    [Fact]
    public async Task Compat_ParserExceptionCleansBookkeepingAndTagsAreCopied()
    {
        // AGH source test: ParserExceptionReleasesAllBookkeepingAndMutableActionsCannotChangeStoredDependencies
        var cache = new ObservationCache<string, string>(clock: () => Now);
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetOrCreateAsync(Request("fail"), (_, _) =>
            throw new InvalidOperationException("parse")));
        Assert.Equal(0, cache.PendingCount);
        Assert.Equal(0, cache.TrackedScopeCount);

        var tags = new HashSet<string>(["changed"]);
        await cache.GetOrCreateAsync(Request("a") with { Tags = tags }, (_, _) =>
            Task.FromResult<ObservationCacheValue<string>?>(new("a", Now)));
        tags.Clear();
        cache.InvalidateTag("changed");
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public async Task Compat_CachedObservationPreservesSameInstanceAndEvidenceId()
    {
        // AGH source test: EvidenceIdRemainsUsableByActualNotebook
        // Notebook integration is consumer-specific; the portable contract is the unchanged observation ID and value.
        var cache = new ObservationCache<string, Observation<string>>(clock: () => Now);
        var id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var observation = new Observation<string>(id, Scope("ctx-a", "run-1", "zone-1", "slot-x"), "subject", "predicate", "value", "source", "kind", .9, Now);

        var result = await cache.GetOrCreateAsync(Request("a"), (_, _) =>
            Task.FromResult<ObservationCacheValue<Observation<string>>?>(new(observation, Now, observation.Id)));

        Assert.Same(observation, result.Value);
        Assert.Equal(id, result.Evidence.EvidenceId);
    }

    [Fact]
    public async Task Compat_MissingInputInvalidatesEarlierReuse()
    {
        // AGH source test: MissingInputInvalidatesEarlierReuse
        var cache = new ObservationCache<string, string>(clock: () => Now);
        var calls = 0;
        Task<ObservationCacheValue<string>?> Parse(string input, CancellationToken _)
        {
            calls++;
            return Task.FromResult<ObservationCacheValue<string>?>(new("known", Now));
        }

        await cache.GetOrCreateAsync(Request("a"), Parse);
        var missing = await cache.GetOrCreateAsync(Request("a") with { Input = null! }, Parse);
        await cache.GetOrCreateAsync(Request("a"), Parse);

        Assert.Null(missing.Value);
        Assert.Equal(ObservationCacheDisposition.Unknown, missing.Evidence.Disposition);
        Assert.Equal(2, calls);
    }

    private static ObservationCacheRequest<string> Request(string input) =>
        new(input, Scope("ctx-a", "run-1", "zone-1", "slot-x"), input, "v1", TimeSpan.FromMinutes(1), new HashSet<string>(["changed"]));

    private static ObservationScope Scope(params string[] segments) => ObservationScope.Of(segments);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Yield();
        }

        Assert.True(condition());
    }
}
