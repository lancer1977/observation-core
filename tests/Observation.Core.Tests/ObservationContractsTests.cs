using System.Text.Json;
using Observation.Core;
using Xunit;

namespace Observation.Core.Tests;

public sealed class ObservationContractsTests
{
    private static readonly Guid Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset ObservedAt = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ScopeValidatesSegmentsAndCopiesInput()
    {
        Assert.Throws<ArgumentNullException>(() => ObservationScope.Of(null!));
        Assert.Throws<ArgumentException>(() => ObservationScope.Of());
        Assert.Throws<ArgumentException>(() => ObservationScope.Of("a", " "));

        var input = new[] { "a", "b" };
        var scope = ObservationScope.Of(input);
        input[0] = "changed";

        Assert.Equal(new[] { "a", "b" }, scope.Segments);
        Assert.Throws<ArgumentException>(() => scope.Append("\t"));
    }

    [Fact]
    public void ScopeUsesOrdinalElementWiseSemantics()
    {
        var slashSeparated = ObservationScope.Of("a", "b/c");
        var differentSegments = ObservationScope.Of("a/b", "c");
        var concatenated = ObservationScope.Of("ab");
        var separate = ObservationScope.Of("a", "b");

        Assert.NotEqual(slashSeparated, differentSegments);
        Assert.NotEqual(separate, concatenated);
        Assert.NotEqual(slashSeparated.GetHashCode(), differentSegments.GetHashCode());
        Assert.True(ObservationScope.Of("a", "b").CompareTo(ObservationScope.Of("a", "c")) < 0);
        Assert.True(ObservationScope.Of("a").CompareTo(ObservationScope.Of("a", "b")) < 0);
    }

    [Fact]
    public void ScopeSupportsPrefixAndAppend()
    {
        var scope = ObservationScope.Of("root", "child");

        Assert.True(scope.IsPrefixedBy(ObservationScope.Of("root")));
        Assert.True(scope.IsPrefixedBy(scope));
        Assert.False(scope.IsPrefixedBy(ObservationScope.Of("other")));
        Assert.False(ObservationScope.Of("root").IsPrefixedBy(scope));
        Assert.Equal(new[] { "root", "child", "leaf" }, scope.Append("leaf").Segments);
    }

    [Fact]
    public void ObservationValidatesEveryConstructorRule()
    {
        Assert.Throws<ArgumentException>(() => Create(id: Guid.Empty));
        Assert.Throws<ArgumentNullException>(() => new Observation<string>(Id, null!, "subject", "predicate", "value", "source", "kind", .5, ObservedAt));
        Assert.Throws<ArgumentException>(() => Create(subject: " "));
        Assert.Throws<ArgumentException>(() => Create(predicate: ""));
        Assert.Throws<ArgumentNullException>(() => new Observation<string>(Id, ObservationScope.Of("root"), "subject", "predicate", null!, "source", "kind", .5, ObservedAt));
        Assert.Throws<ArgumentException>(() => Create(sourceId: "\t"));
        Assert.Throws<ArgumentException>(() => Create(sourceKind: ""));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(confidence: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(confidence: double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(confidence: double.NegativeInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(confidence: -0.01));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(confidence: 1.01));
        Assert.Throws<ArgumentException>(() => new Observation<string>(Id, ObservationScope.Of("root"), "subject", "predicate", "value", "source", "kind", .5, default));

        Assert.Equal(0, Create(confidence: 0).Confidence);
        Assert.Equal(1, Create(confidence: 1).Confidence);
    }

    [Fact]
    public void DomainNeutralUsageSupportsDifferentLocalRecordTypes()
    {
        var game = new Observation<GameState>(Id, ObservationScope.Of("session", "one"), "state", "is", new GameState(7), "source-1", "sensor", .9, ObservedAt);
        var broadcast = new Observation<BroadcastState>(Id, ObservationScope.Of("channel", "one"), "broadcast", "is", new BroadcastState("ready"), "source-2", "feed", .8, ObservedAt);

        Assert.Equal(7, game.Value.Score);
        Assert.Equal("ready", broadcast.Value.Status);
    }

    [Fact]
    public void ObservationRoundTripsThroughJson()
    {
        var original = new Observation<string>(Id, ObservationScope.Of("root", "a/b"), "subject", "predicate", "value", "source", "opaque", .75, ObservedAt);

        var json = JsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<Observation<string>>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(original, roundTripped);
        Assert.Equal(original.Scope.Segments, roundTripped.Scope.Segments);
    }

    private static Observation<string> Create(
        Guid? id = null,
        ObservationScope? scope = null,
        string subject = "subject",
        string predicate = "predicate",
        string? value = "value",
        string sourceId = "source",
        string sourceKind = "kind",
        double confidence = .5,
        DateTimeOffset? observedAt = null) =>
        new(id ?? Id, scope ?? ObservationScope.Of("root"), subject, predicate, value!, sourceId, sourceKind, confidence, observedAt ?? ObservedAt);

    private sealed record GameState(int Score);

    private sealed record BroadcastState(string Status);
}
