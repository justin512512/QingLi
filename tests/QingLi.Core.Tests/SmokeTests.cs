using System.Reflection;

namespace QingLi.Core.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void Core_assembly_has_expected_identity()
    {
        var assembly = typeof(QingLi.Core.AssemblyMarker).Assembly;

        Assert.Equal("QingLi.Core", assembly.GetName().Name);
        Assert.Equal(new Version(0, 1, 1, 0), assembly.GetName().Version);
        Assert.Equal("QingLi.Core", assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product);
    }
}
