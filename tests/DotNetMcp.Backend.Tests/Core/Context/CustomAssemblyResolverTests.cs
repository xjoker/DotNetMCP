using DotNetMcp.Backend.Core.Context;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Context;

public class CustomAssemblyResolverTests
{
    [Fact]
    public void Constructor_WithValidSearchPaths_ShouldNotThrow()
    {
        // Arrange
        var searchPaths = new[] { Path.GetTempPath() };

        // Act
        using var resolver = new CustomAssemblyResolver(searchPaths);

        // Assert
        Assert.NotNull(resolver);
        Assert.NotNull(resolver.Statistics);
    }

    [Fact]
    public void Statistics_InitialState_ShouldBeZero()
    {
        // Arrange & Act
        using var resolver = new CustomAssemblyResolver();

        // Assert
        Assert.Equal(0, resolver.Statistics.TotalAttempts);
        Assert.Equal(0, resolver.Statistics.Level1Success);
        Assert.Equal(0, resolver.Statistics.Level2Success);
        Assert.Equal(0, resolver.Statistics.Level3Fallback);
    }

    [Fact]
    public void GetSuccessRate_WithNoAttempts_ShouldReturnZero()
    {
        // Arrange
        using var resolver = new CustomAssemblyResolver();

        // Act
        var successRate = resolver.GetSuccessRate();

        // Assert
        Assert.Equal(0, successRate);
    }

    [Fact]
    public void Dispose_ShouldClearCache()
    {
        // Arrange
        using var resolver = new CustomAssemblyResolver();

        // Act & Assert - 不应抛出异常
        resolver.Dispose();
    }
}
