using System.Globalization;
using Xunit;

namespace Observation.Core.Tests.Fusion;

public sealed class ObservationFusionOrderingTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly ObservationScope Scope = ObservationScope.Of("root");

    [Theory]
    [InlineData("en-US")]
    [InlineData("sv-SE")]
    [InlineData("")]
    public void DefaultStringValueOrderIsOrdinalRegardlessOfCulture(string cultureName)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            var lower = Create("a");
            var upper = Create("B");

            var result = new ObservationFusion<string>().Resolve([lower, upper]).Single();
            var reversed = new ObservationFusion<string>().Resolve([upper, lower]).Single();

            Assert.Equal(FusionStatus.Disputed, result.Status);
            Assert.Equal(["B", "a"], result.Candidates.Select(candidate => candidate.Value));
            Assert.Equal(result.Candidates.Select(candidate => candidate.Value), reversed.Candidates.Select(candidate => candidate.Value));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void SuppliedValueOrderStillOverridesTheStringDefault()
    {
        var options = new ObservationFusionOptions<string>(valueOrder: StringComparer.OrdinalIgnoreCase);

        var result = new ObservationFusion<string>(options).Resolve([Create("B"), Create("a")]).Single();

        Assert.Equal(["a", "B"], result.Candidates.Select(candidate => candidate.Value));
    }

    private static Observation<string> Create(string value) => new(
        Guid.NewGuid(), Scope, "subject", "predicate", value, "source", "kind", .5, Now);
}
