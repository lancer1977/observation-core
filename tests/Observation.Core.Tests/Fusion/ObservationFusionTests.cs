using Observation.Core;
using Xunit;

namespace Observation.Core.Tests.Fusion;

public sealed class ObservationFusionTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly ObservationScope Scope = ObservationScope.Of("root");

    [Fact]
    public void Resolve_EmptyInputReturnsNoResults()
    {
        Assert.Empty(new ObservationFusion<string>().Resolve([]));
    }

    [Fact]
    public void Resolve_AgreementAveragesConfidenceAndPreservesEvidence()
    {
        var first = Observation("open", .6, id: 2);
        var second = Observation("open", .8, id: 1);

        var result = new ObservationFusion<string>().Resolve([first, second]).Single();

        Assert.Equal(FusionStatus.Agreed, result.Status);
        Assert.Equal("open", result.Value);
        Assert.Equal(.7, result.Confidence, 10);
        Assert.Equal([second.Id, first.Id], result.EvidenceIds);
        Assert.Equal([second.Id, first.Id], result.Candidates.Single().EvidenceIds);
    }

    [Fact]
    public void Resolve_ResolvesAtExactMarginAndDisputesNearTie()
    {
        var exact = new ObservationFusion<string>().Resolve([
            Observation("open", .9), Observation("closed", .7)]).Single();
        var near = new ObservationFusion<string>().Resolve([
            Observation("open", .9), Observation("closed", .700001)]).Single();

        Assert.Equal(FusionStatus.Resolved, exact.Status);
        Assert.Equal("open", exact.Value);
        Assert.Equal(FusionStatus.Disputed, near.Status);
        Assert.Null(near.Value);
        Assert.Equal(2, near.Candidates.Count);
        Assert.Equal(2, near.EvidenceIds.Count);
    }

    [Fact]
    public void Resolve_ThreeWayConflictRetainsEveryCandidate()
    {
        var observations = new[] { Observation(1, .95), Observation(2, .7), Observation(3, .6) };

        var result = new ObservationFusion<int>().Resolve(observations).Single();

        Assert.Equal(FusionStatus.Resolved, result.Status);
        Assert.Equal(1, result.Value);
        Assert.Equal(3, result.Candidates.Count);
        Assert.Equal(observations.Select(item => item.Id).Order(), result.EvidenceIds);
    }

    [Fact]
    public void Resolve_OrdersCandidatesByValueWhenConfidenceTies()
    {
        var result = new ObservationFusion<int>().Resolve([
            Observation(2, .8), Observation(1, .8)]).Single();

        Assert.Equal([1, 2], result.Candidates.Select(candidate => candidate.Value));
        Assert.Equal(FusionStatus.Disputed, result.Status);
    }

    [Fact]
    public void Resolve_IsIndependentOfInputOrderAndUsesStableGroupOrder()
    {
        var observations = new[]
        {
            Observation("b", .8, scope: ObservationScope.Of("z"), subject: "subject-2"),
            Observation("a", .8, scope: ObservationScope.Of("a"), subject: "subject-2"),
            Observation("b", .7, scope: ObservationScope.Of("a"), subject: "subject-1"),
            Observation("a", .9, scope: ObservationScope.Of("a"), subject: "subject-1")
        };

        var first = new ObservationFusion<string>().Resolve(observations);
        var second = new ObservationFusion<string>().Resolve(observations.Reverse());

        Assert.Equal(first.Count, second.Count);
        for (var index = 0; index < first.Count; index++)
        {
            Assert.Equal(first[index].Scope, second[index].Scope);
            Assert.Equal(first[index].Subject, second[index].Subject);
            Assert.Equal(first[index].Predicate, second[index].Predicate);
            Assert.Equal(first[index].Value, second[index].Value);
            Assert.Equal(first[index].Confidence, second[index].Confidence);
            Assert.Equal(first[index].Status, second[index].Status);
            Assert.Equal(first[index].EvidenceIds, second[index].EvidenceIds);
            Assert.Equal(first[index].ExcludedEvidenceIds, second[index].ExcludedEvidenceIds);
            Assert.Equal(first[index].Candidates.Select(candidate => candidate.Value), second[index].Candidates.Select(candidate => candidate.Value));
            Assert.Equal(first[index].Candidates.Select(candidate => candidate.Confidence), second[index].Candidates.Select(candidate => candidate.Confidence));
            Assert.Equal(first[index].Candidates.SelectMany(candidate => candidate.EvidenceIds), second[index].Candidates.SelectMany(candidate => candidate.EvidenceIds));
        }
        Assert.Equal(["a", "a", "z"], first.Select(result => result.Scope.Segments[0]));
        Assert.Equal(["subject-1", "subject-2", "subject-2"], first.Select(result => result.Subject));
    }

    [Fact]
    public void Resolve_IsolatesScopesAndDoesNotMergePrefixes()
    {
        var result = new ObservationFusion<string>().Resolve([
            Observation("open", .8, scope: ObservationScope.Of("root")),
            Observation("closed", .8, scope: ObservationScope.Of("root", "child")),
            Observation("open", .8, scope: ObservationScope.Of("other"))]);

        Assert.Equal(3, result.Count);
        Assert.All(result, item => Assert.Equal(FusionStatus.Agreed, item.Status));
    }

    [Fact]
    public void Resolve_ExcludesFutureExpiredAndExactlyMaxAgeEvidence()
    {
        var valid = Observation("valid", .9, observedAt: Now.AddSeconds(-9));
        var expired = Observation("expired", .9, observedAt: Now.AddSeconds(-10));
        var future = Observation("future", .9, observedAt: Now.AddSeconds(1));
        var options = new ObservationFusionOptions<string>(maxAge: TimeSpan.FromSeconds(10), clock: () => Now);

        var result = new ObservationFusion<string>(options).Resolve([future, expired, valid]).Single();

        Assert.Equal(FusionStatus.Agreed, result.Status);
        Assert.Equal("valid", result.Value);
        Assert.Equal([valid.Id], result.EvidenceIds);
        Assert.Equal(new[] { expired.Id, future.Id }.Order(), result.ExcludedEvidenceIds);
    }

    [Fact]
    public void Resolve_AllExcludedEvidenceIsUnknown()
    {
        var evidence = Observation("old", .9, observedAt: Now.AddMinutes(-1));
        var options = new ObservationFusionOptions<string>(maxAge: TimeSpan.FromSeconds(1), clock: () => Now);

        var result = new ObservationFusion<string>(options).Resolve([evidence]).Single();

        Assert.Equal(FusionStatus.Unknown, result.Status);
        Assert.Null(result.Value);
        Assert.Equal(0, result.Confidence);
        Assert.Empty(result.EvidenceIds);
        Assert.Empty(result.Candidates);
        Assert.Equal([evidence.Id], result.ExcludedEvidenceIds);
    }

    [Fact]
    public void Resolve_UsesCustomEqualityAndOrderingComparers()
    {
        var options = new ObservationFusionOptions<string>(
            valueEquality: StringComparer.OrdinalIgnoreCase,
            valueOrder: StringComparer.OrdinalIgnoreCase);

        var result = new ObservationFusion<string>(options).Resolve([
            Observation("BETA", .8), Observation("alpha", .8)]).Single();

        Assert.Equal("alpha", result.Candidates[0].Value);
        Assert.Equal(FusionStatus.Disputed, result.Status);
    }

    [Fact]
    public void Resolve_RejectsNullInputAndElements()
    {
        var fusion = new ObservationFusion<string>();
        Assert.Throws<ArgumentNullException>(() => fusion.Resolve(null!));
        Assert.Throws<ArgumentException>(() => fusion.Resolve(new Observation<string>[] { null! }));
    }

    [Fact]
    public void Options_RejectInvalidMarginsAndAges()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ObservationFusionOptions<string>(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ObservationFusionOptions<string>(-0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ObservationFusionOptions<string>(maxAge: TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public void Fusion_RequiresOrderingForUnorderedValuesUnlessSupplied()
    {
        Assert.Throws<InvalidOperationException>(() => new ObservationFusion<UnorderedValue>());
        var options = new ObservationFusionOptions<UnorderedValue>(valueOrder: Comparer<UnorderedValue>.Create(
            (left, right) => left.Name.CompareTo(right.Name, StringComparison.Ordinal)));
        var fusion = new ObservationFusion<UnorderedValue>(options);

        Assert.Single(fusion.Resolve([Observation(new UnorderedValue("value"), .5)]));
    }

    [Fact]
    public void Resolve_DoesNotChangeObservationIds()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var result = new ObservationFusion<string>().Resolve([Observation("value", .5, evidenceId: id)]).Single();

        Assert.Equal(id, result.EvidenceIds.Single());
        Assert.Equal(id, result.Candidates.Single().EvidenceIds.Single());
    }

    private static Observation<string> Observation(
        string value,
        double confidence,
        int? id = null,
        ObservationScope? scope = null,
        string subject = "subject",
        string predicate = "predicate",
        DateTimeOffset? observedAt = null,
        Guid? evidenceId = null) => new(
            id is null ? evidenceId ?? Guid.NewGuid() : Guid.Parse($"00000000-0000-0000-0000-{id.Value:D12}"),
            scope ?? Scope,
            subject,
            predicate,
            value,
            "source",
            "kind",
            confidence,
            observedAt ?? Now);

    private static Observation<int> Observation(int value, double confidence) => new(
        Guid.NewGuid(), Scope, "subject", "predicate", value, "source", "kind", confidence, Now);

    private static Observation<UnorderedValue> Observation(UnorderedValue value, double confidence) => new(
        Guid.NewGuid(), Scope, "subject", "predicate", value, "source", "kind", confidence, Now);

    private sealed record UnorderedValue(string Name);
}
