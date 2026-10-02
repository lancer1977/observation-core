using Observation.Core;
using Xunit;

namespace Observation.Core.Tests.Mutation;

public sealed class FusionAndCacheMutationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly ObservationScope Scope = ObservationScope.Of("root");

    [Fact]
    public void FusionOptionsAcceptBothConfidenceBoundariesAndKeepDefaultPolicies()
    {
        var zero = new ObservationFusionOptions<string>(minimumConfidenceMargin: 0);
        var one = new ObservationFusionOptions<string>(minimumConfidenceMargin: 1);
        var noExpiry = new ObservationFusionOptions<string>();

        Assert.Equal(0, zero.MinimumConfidenceMargin);
        Assert.Equal(1, one.MinimumConfidenceMargin);
        Assert.Null(noExpiry.Clock);
        Assert.Equal("same", new ObservationFusion<string>(noExpiry).Resolve([
            Observation("same", .5, 1), Observation("same", .5, 2)]).Single().Value);
    }

    [Fact]
    public void FusionUsesCustomEqualityToConstructOneCandidate()
    {
        var options = new ObservationFusionOptions<string>(
            valueEquality: StringComparer.OrdinalIgnoreCase,
            valueOrder: StringComparer.OrdinalIgnoreCase);

        var result = new ObservationFusion<string>(options).Resolve([
            Observation("OPEN", .4, 1), Observation("open", .6, 2)]).Single();

        var candidate = Assert.Single(result.Candidates);
        Assert.Equal("OPEN", candidate.Value);
        Assert.Equal(.5, candidate.Confidence, 10);
        Assert.Equal(FusionStatus.Agreed, result.Status);
    }

    [Fact]
    public void FusionUsesInclusiveResolutionMarginAndStableResultConstructionOrder()
    {
        var fusion = new ObservationFusion<string>(new(minimumConfidenceMargin: 0));
        var resolved = fusion.Resolve([
            Observation("a", .5, 2), Observation("b", .5, 1)]);
        var ordered = fusion.Resolve([
            Observation("a", .5, 2, predicate: "z"), Observation("b", .5, 1, predicate: "a")]);

        Assert.Equal(["a", "z"], ordered.Select(result => result.Predicate));
        Assert.Equal(["a", "b"], resolved.Single().Candidates.Select(candidate => candidate.Value));
        Assert.All(resolved, result => Assert.Equal(FusionStatus.Resolved, result.Status));
    }

    [Fact]
    public async Task CacheHitUnionsTagsSoTheNewTagInvalidatesTheEntry()
    {
        var cache = new ObservationCache<string, string>(clock: () => Now);
        await cache.GetOrCreateAsync(Request("value", "first"), (_, _) =>
            Task.FromResult<ObservationCacheValue<string>?>(new("value", Now)));
        await cache.GetOrCreateAsync(Request("value", "second"), (_, _) =>
            throw new Xunit.Sdk.XunitException("must hit"));

        cache.InvalidateTag("second");

        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public async Task NullParserOutputIsUnknownAndDoesNotEnterTheCache()
    {
        var cache = new ObservationCache<string, string>(clock: () => Now);

        var result = await cache.GetOrCreateAsync(Request("null"), (_, _) =>
            Task.FromResult<ObservationCacheValue<string>?>(null));

        Assert.Null(result.Value);
        Assert.Equal(ObservationCacheDisposition.Unknown, result.Evidence.Disposition);
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public async Task FifoEvictionHappensWhenTheQueueReachesCapacity()
    {
        var cache = new ObservationCache<string, string>(1, () => Now);
        await cache.GetOrCreateAsync(Request("one"), (_, _) => Value("one"));
        Assert.Equal(1, cache.Count);
        await cache.GetOrCreateAsync(Request("two"), (_, _) => Value("two"));
        Assert.Equal(1, cache.Count);

        var calls = 0;
        var result = await cache.GetOrCreateAsync(Request("one"), (_, _) =>
        {
            calls++;
            return Value("one-again");
        });

        Assert.Equal(1, calls);
        Assert.Equal("one-again", result.Value);
    }

    [Fact]
    public async Task ParserCapacityRejectsTheRequestAtTheCapacityBoundary()
    {
        var cache = new ObservationCache<string, string>(1, () => Now);
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource<ObservationCacheValue<string>?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var parsing = cache.GetOrCreateAsync(Request("first"), (_, _) =>
        {
            started.SetResult(true);
            return gate.Task;
        });

        await started.Task;
        var rejected = await cache.GetOrCreateAsync(Request("second") with { Scope = ObservationScope.Of("other") }, (_, _) => Value("second"));

        Assert.Equal(ObservationCacheDisposition.Unknown, rejected.Evidence.Disposition);
        gate.SetResult(new("first", Now));
        await parsing;
    }

    [Fact]
    public async Task ScopeInvalidationCancelsAPendingParserThatObservesItsToken()
    {
        var cache = new ObservationCache<string, string>(clock: () => Now);
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var parsing = cache.GetOrCreateAsync(Request("pending") with { Scope = ObservationScope.Of("scope", "child") }, (_, token) =>
        {
            started.SetResult(true);
            var gate = new TaskCompletionSource<ObservationCacheValue<string>?>(TaskCreationOptions.RunContinuationsAsynchronously);
            token.Register(() => gate.TrySetCanceled(token));
            return gate.Task;
        });

        await started.Task;
        cache.InvalidateScope(ObservationScope.Of("scope"));

        var result = await parsing;
        Assert.Equal(ObservationCacheDisposition.Invalidated, result.Evidence.Disposition);
        Assert.Equal(0, cache.PendingCount);
    }

    private static ObservationCacheRequest<string> Request(string input, params string[] tags) =>
        new(input, Scope, input, "v1", TimeSpan.FromMinutes(1), new HashSet<string>(tags));

    private static Task<ObservationCacheValue<string>?> Value(string value) =>
        Task.FromResult<ObservationCacheValue<string>?>(new(value, Now));

    private static Observation<string> Observation(string value, double confidence, int id, string predicate = "predicate") =>
        new(Guid.Parse($"00000000-0000-0000-0000-{id:D12}"), Scope, "subject", predicate,
            value, "source", "kind", confidence, Now);
}
