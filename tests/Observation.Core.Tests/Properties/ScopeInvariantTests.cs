using Xunit;

namespace Observation.Core.Tests.Properties;

public sealed class ScopeInvariantTests
{
    [Fact]
    public void SeededScopeInvariantsHoldForAtLeastTwoHundredSeeds()
    {
        for (var seed = 0; seed < 256; seed++)
        {
            try
            {
                Check(seed);
            }
            catch (Exception exception)
            {
                throw new Xunit.Sdk.XunitException($"Scope invariant failed for seed {seed}.", exception);
            }
        }
    }

    [Fact]
    public void NegativeControl_InvertedPrefixRelationIsDetected()
    {
        var exception = Assert.ThrowsAny<Exception>(() => AssertPrefix(23, deliberatelyInvert: true));
        Assert.Contains("seed 23", exception.Message);
    }

    private static void Check(int seed)
    {
        var random = new Random(seed);
        var scopes = Enumerable.Range(0, 12)
            .Select(_ => ObservationScope.Of(Enumerable.Range(0, random.Next(1, 5))
                .Select(segment => $"{(char)('a' + random.Next(0, 4))}-{segment}").ToArray()))
            .ToArray();
        foreach (var left in scopes)
        foreach (var right in scopes)
        {
            Assert.Equal(left.Equals(right), left.CompareTo(right) == 0);
            Assert.Equal(Math.Sign(left.CompareTo(right)), -Math.Sign(right.CompareTo(left)));
            Assert.Equal(left.IsPrefixedBy(right), IsPrefix(left, right));
        }

        foreach (var scope in scopes)
        {
            var child = scope.Append("child");
            Assert.True(child.IsPrefixedBy(scope));
            Assert.True(scope.IsPrefixedBy(scope));
            Assert.False(scope.IsPrefixedBy(child));
        }

        AssertPrefix(seed, deliberatelyInvert: false);
    }

    private static void AssertPrefix(int seed, bool deliberatelyInvert)
    {
        var parent = ObservationScope.Of("root", $"seed-{seed}");
        var child = parent.Append("child");
        var actual = deliberatelyInvert ? parent.IsPrefixedBy(child) : child.IsPrefixedBy(parent);
        Assert.True(actual, $"seed {seed}: prefix relation was inconsistent.");
    }

    private static bool IsPrefix(ObservationScope scope, ObservationScope prefix) =>
        prefix.Segments.Count <= scope.Segments.Count &&
        prefix.Segments.SequenceEqual(scope.Segments.Take(prefix.Segments.Count));
}
