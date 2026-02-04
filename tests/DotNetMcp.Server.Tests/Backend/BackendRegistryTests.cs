using DotNetMcp.Server.Backend;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Server.Tests.Backend;

/// <summary>
/// BackendRegistry 单元测试
/// </summary>
public class BackendRegistryTests
{
    private readonly Mock<ILogger<BackendRegistry>> _mockLogger;

    public BackendRegistryTests()
    {
        _mockLogger = new Mock<ILogger<BackendRegistry>>();
    }

    private BackendRegistry CreateRegistry() => new BackendRegistry(_mockLogger.Object);

    [Fact]
    public void Register_WithNewBackend_AddsToRegistry()
    {
        // Arrange
        var registry = CreateRegistry();
        var mockBackend = new Mock<IBackend>();
        mockBackend.Setup(b => b.Id).Returns("test-backend");

        // Act
        registry.Register(mockBackend.Object);

        // Assert
        Assert.Equal(1, registry.Count);
        Assert.NotNull(registry.Get("test-backend"));
    }

    [Fact]
    public void Register_FirstBackend_BecomesDefault()
    {
        // Arrange
        var registry = CreateRegistry();
        var mockBackend = new Mock<IBackend>();
        mockBackend.Setup(b => b.Id).Returns("first-backend");

        // Act
        registry.Register(mockBackend.Object);

        // Assert
        Assert.Equal("first-backend", registry.DefaultBackendId);
    }

    [Fact]
    public void Unregister_WithExistingBackend_RemovesFromRegistry()
    {
        // Arrange
        var registry = CreateRegistry();
        var mockBackend = new Mock<IBackend>();
        mockBackend.Setup(b => b.Id).Returns("to-remove");
        registry.Register(mockBackend.Object);

        // Act
        var result = registry.Unregister("to-remove");

        // Assert
        Assert.True(result);
        Assert.Equal(0, registry.Count);
    }

    [Fact]
    public void Unregister_WithNonExistentBackend_ReturnsFalse()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.Unregister("nonexistent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Get_WithNullId_ReturnsDefaultBackend()
    {
        // Arrange
        var registry = CreateRegistry();
        var mockBackend = new Mock<IBackend>();
        mockBackend.Setup(b => b.Id).Returns("default");
        registry.Register(mockBackend.Object);
        registry.SetDefault("default");

        // Act
        var result = registry.Get(null);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("default", result.Id);
    }

    [Fact]
    public void Get_WithSpecificId_ReturnsCorrectBackend()
    {
        // Arrange
        var registry = CreateRegistry();
        var backend1 = new Mock<IBackend>();
        backend1.Setup(b => b.Id).Returns("backend1");
        var backend2 = new Mock<IBackend>();
        backend2.Setup(b => b.Id).Returns("backend2");

        registry.Register(backend1.Object);
        registry.Register(backend2.Object);

        // Act
        var result = registry.Get("backend2");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("backend2", result.Id);
    }

    [Fact]
    public void GetAll_ReturnsAllBackends()
    {
        // Arrange
        var registry = CreateRegistry();
        var backend1 = new Mock<IBackend>();
        backend1.Setup(b => b.Id).Returns("b1");
        var backend2 = new Mock<IBackend>();
        backend2.Setup(b => b.Id).Returns("b2");

        registry.Register(backend1.Object);
        registry.Register(backend2.Object);

        // Act
        var result = registry.GetAll();

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void SetDefault_WithExistingBackend_SetsDefault()
    {
        // Arrange
        var registry = CreateRegistry();
        var mockBackend = new Mock<IBackend>();
        mockBackend.Setup(b => b.Id).Returns("new-default");
        registry.Register(mockBackend.Object);

        // Act
        var result = registry.SetDefault("new-default");

        // Assert
        Assert.True(result);
        Assert.Equal("new-default", registry.DefaultBackendId);
    }

    [Fact]
    public void SetDefault_WithNonExistentBackend_ReturnsFalse()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.SetDefault("nonexistent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void DefaultBackendId_WhenNoDefault_ReturnsNull()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act & Assert
        Assert.Null(registry.DefaultBackendId);
    }
}
