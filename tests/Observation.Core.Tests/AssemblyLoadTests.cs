using System.Reflection;
using Xunit;

namespace Observation.Core.Tests;

public sealed class AssemblyLoadTests
{
    [Fact]
    public void ObservationCoreAssemblyLoads()
    {
        var assembly = Assembly.Load("Observation.Core");

        Assert.Equal("Observation.Core", assembly.GetName().Name);
    }
}
