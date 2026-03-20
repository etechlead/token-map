using Clever.TokenMap.Core;

namespace Clever.TokenMap.Core.Tests;

public sealed class CoreAssemblyMarkerTests
{
    [Fact]
    public void CoreAssemblyMarker_ResolvesCoreAssembly()
    {
        var assemblyName = typeof(CoreAssemblyMarker).Assembly.GetName().Name;

        Assert.Equal("Clever.TokenMap.Core", assemblyName);
    }
}
