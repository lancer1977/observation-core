using Observation.Core;
using Xunit;

namespace Observation.Core.Tests.Docs;

public sealed class ConsumerExamplesTests
{
    // These examples are kept identical to the code blocks in docs/consumers.md.
    [Fact]
    public void CreateObservationExampleMatchesDocumentedContract()
    {
        var observedAt = new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
        var observation = new Observation<string>(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ObservationScope.Of("sensor", "north"),
            "door",
            "state",
            "open",
            "reader-1",
            "detector",
            0.9,
            observedAt);

        Assert.Equal("open", observation.Value);
        Assert.Equal(0.9, observation.Confidence);
        Assert.Equal(observedAt, observation.ObservedAt);
    }

    [Fact]
    public void FuseObservationsExampleProducesAgreedMean()
    {
        var observedAt = new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
        var scope = ObservationScope.Of("sensor", "north");
        var observations = new[]
        {
            new Observation<string>(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                scope, "door", "state", "open", "reader-1", "detector", 0.8, observedAt),
            new Observation<string>(
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                scope, "door", "state", "open", "reader-2", "detector", 0.9, observedAt)
        };

        var result = new ObservationFusion<string>().Resolve(observations).Single();

        Assert.Equal(FusionStatus.Agreed, result.Status);
        Assert.Equal("open", result.Value);
        Assert.Equal(0.85, result.Confidence, 10);
        Assert.Equal(2, result.EvidenceIds.Count);
    }

    [Fact]
    public async Task CacheExamplePreservesValueAndEvidenceId()
    {
        var observedAt = new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
        var observation = new Observation<string>(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ObservationScope.Of("sensor", "north"),
            "door", "state", "open", "reader-1", "detector", 0.9, observedAt);
        var cache = new ObservationCache<string, Observation<string>>(clock: () => observedAt);
        var request = new ObservationCacheRequest<string>(
            "frame-001", observation.Scope, "sha256:frame-001", "detector-1", TimeSpan.FromMinutes(1));
        var cached = await cache.GetOrCreateAsync(
            request,
            (_, _) => Task.FromResult<ObservationCacheValue<Observation<string>>?>(
                new(observation, observation.ObservedAt, observation.Id)));

        Assert.Equal(ObservationCacheDisposition.Miss, cached.Evidence.Disposition);
        Assert.Same(observation, cached.Value);
        Assert.Equal(observation.Id, cached.Evidence.EvidenceId);
    }
}
