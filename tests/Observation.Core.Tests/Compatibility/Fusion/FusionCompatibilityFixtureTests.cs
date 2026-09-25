using System.Text.Json;
using Observation.Core;
using Xunit;

namespace Observation.Core.Tests.Compatibility.Fusion;

public sealed class FusionCompatibilityFixtureTests
{
    private static readonly string FixturesDirectory = Path.Combine(AppContext.BaseDirectory, "Compatibility", "Fusion", "Fixtures");
    private static readonly JsonSerializerOptions FixtureJsonOptions = new() { PropertyNameCaseInsensitive = true };
    // Deterministic reorderings for a fixture of any size: as-authored, reversed, rotated, and interleaved.
    private static IEnumerable<int[]> PermutationsFor(int count)
    {
        yield return [];
        if (count < 2)
        {
            yield break;
        }

        yield return Enumerable.Range(0, count).Reverse().ToArray();
        yield return Enumerable.Range(1, count - 1).Append(0).ToArray();
        yield return Enumerable.Range(0, count).Where(index => index % 2 == 1)
            .Concat(Enumerable.Range(0, count).Where(index => index % 2 == 0)).ToArray();
    }

    public static IEnumerable<object[]> Fixtures()
    {
        foreach (var path in Directory.EnumerateFiles(FixturesDirectory, "*.json").Order(StringComparer.Ordinal))
        {
            var count = JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("observations").GetArrayLength();
            foreach (var permutation in PermutationsFor(count))
            {
                yield return [path, permutation];
            }
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void ObservationCore_matches_agreement_resolution_and_ordering_fixtures(string path, int[] permutation)
    {
        var fixture = JsonSerializer.Deserialize<Fixture>(File.ReadAllText(path), FixtureJsonOptions)
            ?? throw new InvalidOperationException($"Fixture '{path}' was empty.");
        var observations = fixture.Observations.Select(CreateObservation).ToArray();
        if (permutation.Length > 0)
        {
            observations = permutation.Select(index => observations[index]).ToArray();
        }

        var options = new ObservationFusionOptions<string>(
            fixture.Options.MinimumConfidenceMargin,
            valueEquality: StringComparer.Ordinal,
            valueOrder: StringComparer.Ordinal);
        var actual = new ObservationFusion<string>(options).Resolve(observations);

        Assert.Equal(fixture.ExpectedResults.Count, actual.Count);
        for (var index = 0; index < actual.Count; index++)
        {
            var expected = fixture.ExpectedResults[index];
            var result = actual[index];
            Assert.Equal(expected.Scope, result.Scope.Segments);
            Assert.Equal(expected.Subject, result.Subject);
            Assert.Equal(expected.Predicate, result.Predicate);
            Assert.Equal(Enum.Parse<FusionStatus>(expected.Status), result.Status);
            Assert.Equal(expected.ValueJson, result.Value);
            Assert.Equal(expected.Confidence, result.Confidence, 10);
            Assert.Equal(expected.EvidenceIds.Select(Guid.Parse), result.EvidenceIds);
        }
    }

    private static Observation<string> CreateObservation(ObservationFixture item) => new(
        Guid.Parse(item.Id),
        ObservationScope.Of(item.Scope),
        item.Subject,
        item.Predicate,
        item.ValueJson,
        item.Source,
        "compatibility-fixture",
        item.Confidence,
        DateTimeOffset.Parse(item.ObservedAt));

    private sealed class Fixture
    {
        public required string Name { get; init; }
        public required string AghSourceTestName { get; init; }
        public required FixtureOptions Options { get; init; }
        public required List<ObservationFixture> Observations { get; init; }
        public required List<ExpectedResult> ExpectedResults { get; init; }
    }

    private sealed class FixtureOptions
    {
        public double MinimumConfidenceMargin { get; init; }
    }

    private sealed class ObservationFixture
    {
        public required string Id { get; init; }
        public required string[] Scope { get; init; }
        public required string Subject { get; init; }
        public required string Predicate { get; init; }
        public required string ValueJson { get; init; }
        public required double Confidence { get; init; }
        public required string ObservedAt { get; init; }
        public string Source { get; init; } = "source-a";
    }

    private sealed class ExpectedResult
    {
        public required string[] Scope { get; init; }
        public required string Subject { get; init; }
        public required string Predicate { get; init; }
        public required string Status { get; init; }
        public string? ValueJson { get; init; }
        public required double Confidence { get; init; }
        public required string[] EvidenceIds { get; init; }
    }
}
