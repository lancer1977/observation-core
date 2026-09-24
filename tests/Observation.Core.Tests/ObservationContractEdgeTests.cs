using System.Reflection;
using System.Text.Json;
using Observation.Core;
using Xunit;

namespace Observation.Core.Tests;

public sealed class ObservationContractEdgeTests
{
    private static readonly Guid Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset ObservedAt = new(2026, 9, 24, 8, 30, 0, TimeSpan.FromHours(-4));

    [Fact]
    public void ScopeJsonRoundTripsOpaqueSegmentsAndUsesAnArrayOfStrings()
    {
        var original = ObservationScope.Of("root/branch", "東京", "leaf/終");

        var json = JsonSerializer.Serialize(original);
        using var document = JsonDocument.Parse(json);
        var roundTripped = JsonSerializer.Deserialize<ObservationScope>(json);

        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.All(document.RootElement.EnumerateArray(), element => Assert.Equal(JsonValueKind.String, element.ValueKind));
        Assert.NotNull(roundTripped);
        Assert.Equal(original.Segments, roundTripped.Segments);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[\"a\",\" \"]")]
    public void ScopeJsonRejectsInvalidShapes(string json)
    {
        Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<ObservationScope>(json));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ObservationJsonPreservesOffsetGuidAndConfidenceBoundaries(double confidence)
    {
        var original = new Observation<string>(Id, ObservationScope.Of("root", "a/b"), "subject", "predicate", "value", "source", "opaque", confidence, ObservedAt);

        var roundTripped = JsonSerializer.Deserialize<Observation<string>>(JsonSerializer.Serialize(original));

        Assert.NotNull(roundTripped);
        Assert.Equal(Id, roundTripped.Id);
        Assert.Equal(ObservedAt, roundTripped.ObservedAt);
        Assert.Equal(ObservedAt.Offset, roundTripped.ObservedAt.Offset);
        Assert.Equal(confidence, roundTripped.Confidence);
        Assert.Equal(original, roundTripped);
    }

    [Theory]
    [InlineData("1.5", typeof(ArgumentOutOfRangeException))]
    [InlineData("-0.1", typeof(ArgumentOutOfRangeException))]
    [InlineData("\"00000000-0000-0000-0000-000000000000\"", typeof(ArgumentException))]
    public void ObservationJsonRejectsInvalidConstructorValues(string value, Type expectedException)
    {
        var json = $"{{\"Id\":\"{Id}\",\"Scope\":[\"root\"],\"Subject\":\"subject\",\"Predicate\":\"predicate\",\"Value\":\"value\",\"SourceId\":\"source\",\"SourceKind\":\"kind\",\"Confidence\":{value},\"ObservedAt\":\"2026-09-24T08:30:00-04:00\"}}";

        var exception = Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<Observation<string>>(json));

        Assert.True(expectedException.IsInstanceOfType(exception) || exception is JsonException,
            $"Expected {expectedException.Name} or JsonException, got {exception.GetType().Name}.");
    }

    [Fact]
    public void ObservationJsonRejectsBlankSubject()
    {
        const string json = "{\"Id\":\"22222222-2222-2222-2222-222222222222\",\"Scope\":[\"root\"],\"Subject\":\" \",\"Predicate\":\"predicate\",\"Value\":\"value\",\"SourceId\":\"source\",\"SourceKind\":\"kind\",\"Confidence\":0.5,\"ObservedAt\":\"2026-09-24T08:30:00-04:00\"}";

        var exception = Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<Observation<string>>(json));

        Assert.True(exception is ArgumentException or JsonException,
            $"Expected ArgumentException or JsonException, got {exception.GetType().Name}.");
    }

    [Fact]
    public void ScopeOrderingIsDeterministicAcrossFixedPermutations()
    {
        var expected = new[]
        {
            ObservationScope.Of("a"),
            ObservationScope.Of("a", "b"),
            ObservationScope.Of("a", "b", "c"),
            ObservationScope.Of("a", "c"),
            ObservationScope.Of("b"),
        };

        var permutations = new[]
        {
            new[] { expected[4], expected[1], expected[3], expected[0], expected[2] },
            new[] { expected[2], expected[0], expected[4], expected[3], expected[1] },
            new[] { expected[3], expected[4], expected[2], expected[1], expected[0] },
        };

        foreach (var permutation in permutations)
        {
            Assert.Equal(expected, permutation.OrderBy(scope => scope));
        }

        Assert.True(expected[0].CompareTo(null) > 0);
        Assert.NotEqual(ObservationScope.Of("Case"), ObservationScope.Of("case"));
        Assert.Equal(expected[1], ObservationScope.Of("a", "b"));
        Assert.Equal(expected[1].GetHashCode(), ObservationScope.Of("a", "b").GetHashCode());
    }

    [Fact]
    public void ValueTypesAcceptZeroAndEmptyGuidAndRecordEqualityIsFieldSensitive()
    {
        var integerObservation = new Observation<int>(Id, ObservationScope.Of("root"), "subject", "predicate", 0, "source", "kind", 0, ObservedAt);
        var emptyGuidObservation = new Observation<Guid>(Id, ObservationScope.Of("root"), "subject", "predicate", Guid.Empty, "source", "kind", 0, ObservedAt);
        var equivalent = new Observation<int>(Id, ObservationScope.Of("root"), "subject", "predicate", 0, "source", "kind", 0, ObservedAt);

        Assert.Equal(0, integerObservation.Value);
        Assert.Equal(Guid.Empty, emptyGuidObservation.Value);
        Assert.Equal(integerObservation, equivalent);
        Assert.NotEqual(integerObservation, CreateInt(id: Guid.NewGuid()));
        Assert.NotEqual(integerObservation, CreateInt(scope: ObservationScope.Of("other")));
        Assert.NotEqual(integerObservation, CreateInt(subject: "other"));
        Assert.NotEqual(integerObservation, CreateInt(predicate: "other"));
        Assert.NotEqual(integerObservation, CreateInt(value: 1));
        Assert.NotEqual(integerObservation, CreateInt(sourceId: "other"));
        Assert.NotEqual(integerObservation, CreateInt(sourceKind: "other"));
        Assert.NotEqual(integerObservation, CreateInt(confidence: 1));
        Assert.NotEqual(integerObservation, CreateInt(observedAt: ObservedAt.AddMinutes(1)));
    }

    [Fact]
    public void PublicCoreSurfaceRemainsDomainNeutralAndDependencyBounded()
    {
        var assembly = typeof(ObservationScope).Assembly;
        var forbiddenTerms = new[] { "game", "broadcast", "tv", "media", "ocr", "vlm", "opencv", "ffmpeg", "emulator" };

        var publicNames = assembly.GetExportedTypes()
            .SelectMany(type => new[] { type.Name }.Concat(type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Select(member => member.Name)));

        Assert.DoesNotContain(publicNames, name => forbiddenTerms.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase)));

        var allowedPrefixes = new[] { "System", "Microsoft", "netstandard", "mscorlib", "Observation" };
        Assert.All(assembly.GetReferencedAssemblies(), reference =>
            Assert.Contains(allowedPrefixes, prefix => reference.Name?.StartsWith(prefix, StringComparison.Ordinal) == true));
    }

    private static Observation<int> CreateInt(
        Guid? id = null,
        ObservationScope? scope = null,
        string subject = "subject",
        string predicate = "predicate",
        int value = 0,
        string sourceId = "source",
        string sourceKind = "kind",
        double confidence = 0,
        DateTimeOffset? observedAt = null) =>
        new(id ?? Id, scope ?? ObservationScope.Of("root"), subject, predicate, value, sourceId, sourceKind, confidence, observedAt ?? ObservedAt);
}
