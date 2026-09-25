using System.Text.Json;
using Xunit;

namespace Observation.Core.Tests.Properties;

public sealed class FusionInvariantTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SeededFusionInvariantsHoldForAtLeastTwoHundredSeeds()
    {
        for (var seed = 0; seed < 256; seed++)
        {
            try
            {
                Check(seed);
            }
            catch (Exception exception)
            {
                throw new Xunit.Sdk.XunitException($"Fusion invariant failed for seed {seed}.", exception);
            }
        }
    }

    [Fact]
    public void NegativeControl_ReversedResultOrderIsDetected()
    {
        var exception = Assert.ThrowsAny<Exception>(() =>
            AssertDeterministicOrder(17, deliberatelyReverse: true));
        Assert.NotNull(exception);
    }

    private static void Check(int seed)
    {
        var random = new Random(seed);
        var observations = Enumerable.Range(0, random.Next(1, 25))
            .Select(index => CreateObservation(random, seed, index))
            .ToArray();
        var options = new ObservationFusionOptions<string>(
            minimumConfidenceMargin: .2,
            maxAge: TimeSpan.FromMinutes(5),
            clock: () => Now,
            valueEquality: StringComparer.Ordinal,
            valueOrder: StringComparer.Ordinal);
        var fusion = new ObservationFusion<string>(options);
        var originalResults = fusion.Resolve(observations);
        var expected = Describe(originalResults);

        AssertDeterministicOrder(seed, deliberatelyReverse: false);
        var actual = Describe(fusion.Resolve(observations.OrderBy(_ => random.Next()).ToArray()));
        Assert.Equal(expected, actual);

        var allIds = observations.Select(observation => observation.Id).ToHashSet();
        var resultIds = originalResults.SelectMany(result => result.EvidenceIds.Concat(result.ExcludedEvidenceIds)).ToArray();
        Assert.Equal(allIds.Count, resultIds.Length);
        Assert.Equal(allIds, resultIds.ToHashSet());
        Assert.Equal(resultIds.Length, resultIds.Distinct().Count());

        // Expired (older than MaxAge) and future-dated evidence must be excluded, everything else must be used.
        var expectedExcluded = observations
            .Where(observation => observation.ObservedAt > Now || Now - observation.ObservedAt >= TimeSpan.FromMinutes(5))
            .Select(observation => observation.Id).ToHashSet();
        Assert.True(expectedExcluded.SetEquals(originalResults.SelectMany(result => result.ExcludedEvidenceIds)),
            $"seed {seed}: excluded evidence did not match the expired/future observations.");

        foreach (var result in originalResults)
        {
            Assert.InRange(result.Confidence, 0, 1);
            Assert.Equal(result.EvidenceIds.Order(), result.EvidenceIds);
            Assert.Equal(result.ExcludedEvidenceIds.Order(), result.ExcludedEvidenceIds);
            Assert.Equal(result.Candidates.SelectMany(candidate => candidate.EvidenceIds).Order(), result.EvidenceIds);
            if (result.Candidates.Count > 1)
            {
                Assert.Equal(result.Candidates.Count, result.Candidates.Select(candidate => candidate.Value).Distinct().Count());
                Assert.NotEqual(FusionStatus.Agreed, result.Status);
            }
        }
    }

    private static void AssertDeterministicOrder(int seed, bool deliberatelyReverse)
    {
        var random = new Random(seed);
        var observations = Enumerable.Range(0, 8)
            .Select(index => CreateObservation(random, seed, index))
            .ToArray();
        var fusion = new ObservationFusion<string>(new(
            maxAge: TimeSpan.FromMinutes(5), clock: () => Now,
            valueEquality: StringComparer.Ordinal, valueOrder: StringComparer.Ordinal));
        var actual = fusion.Resolve(observations);
        var reordered = fusion.Resolve(observations.Reverse());
        if (deliberatelyReverse)
        {
            reordered = reordered.Reverse().ToArray(); // Mutation: return results in reverse rather than deterministic order.
        }

        Assert.Equal(Describe(actual), Describe(reordered));
    }

    private static Observation<string> CreateObservation(Random random, int seed, int index)
    {
        var age = random.Next(0, 8) switch
        {
            0 => TimeSpan.FromMinutes(6),
            1 => TimeSpan.FromMinutes(-1),
            _ => TimeSpan.FromSeconds(random.Next(0, 300))
        };
        return new(
            EvidenceId(seed, index),
            ObservationScope.Of($"scope-{random.Next(0, 4)}"),
            $"subject-{random.Next(0, 3)}",
            $"predicate-{random.Next(0, 2)}",
            $"value-{random.Next(0, 3)}",
            "source", "property", random.NextDouble(), Now.Subtract(age));
    }

    private static Guid EvidenceId(int seed, int index) =>
        Guid.Parse($"{seed + 1:D8}-0000-0000-0000-{index + 1:D12}");

    private static string Describe(IReadOnlyList<FusionResult<string>> results) => JsonSerializer.Serialize(results.Select(result => new
    {
        Scope = result.Scope.ToString(), result.Subject, result.Predicate, result.Value,
        result.Confidence, result.Status,
        Evidence = result.EvidenceIds.ToArray(), Excluded = result.ExcludedEvidenceIds.ToArray(),
        Candidates = result.Candidates.Select(candidate => new
        {
            candidate.Value, candidate.Confidence, Evidence = candidate.EvidenceIds.ToArray()
        }).ToArray()
    }));
}
