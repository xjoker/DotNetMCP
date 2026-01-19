using DotNetMcp.Backend.Core.Compilation;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Compilation;

public class ReferenceAssemblyProviderTests
{
    [Fact]
    public void GetReferences_ShouldReturnCoreReferences()
    {
        // Arrange
        var provider = new ReferenceAssemblyProvider();

        // Act
        var references = provider.GetReferences().ToList();

        // Assert
        Assert.NotEmpty(references);
        Assert.True(references.Count >= 5); // 至少包含核心程序集
    }

    [Fact]
    public void GetReferences_ShouldCacheResults()
    {
        // Arrange
        var provider = new ReferenceAssemblyProvider();

        // Act
        var references1 = provider.GetReferences();
        var references2 = provider.GetReferences();

        // Assert
        Assert.Same(references1, references2); // 应该是同一个实例（缓存）
        Assert.Equal(1, provider.GetCachedFrameworkCount());
    }

    [Fact]
    public void GetReferences_DifferentFrameworks_ShouldCacheSeparately()
    {
        // Arrange
        var provider = new ReferenceAssemblyProvider();

        // Act
        var references1 = provider.GetReferences("net9.0");
        var references2 = provider.GetReferences("net8.0");

        // Assert
        Assert.Equal(2, provider.GetCachedFrameworkCount());
    }

    [Fact]
    public void ClearCache_ShouldRemoveAllCachedReferences()
    {
        // Arrange
        var provider = new ReferenceAssemblyProvider();
        provider.GetReferences("net9.0");
        provider.GetReferences("net8.0");
        Assert.Equal(2, provider.GetCachedFrameworkCount());

        // Act
        provider.ClearCache();

        // Assert
        Assert.Equal(0, provider.GetCachedFrameworkCount());
    }
}
