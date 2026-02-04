using DotNetMcp.Server.Backend;
using DotNetMcp.Server.Tools;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Server.Tests.Tools;

/// <summary>
/// InstanceTools 单元测试
/// </summary>
public class InstanceToolsTests
{
    private readonly Mock<IBackendRegistry> _mockRegistry;
    private readonly Mock<IBackend> _mockBackend;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<RemoteBackend>> _mockLogger;
    private readonly InstanceTools _tools;

    public InstanceToolsTests()
    {
        _mockRegistry = new Mock<IBackendRegistry>();
        _mockBackend = new Mock<IBackend>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<RemoteBackend>>();

        _mockBackend.Setup(b => b.Id).Returns("local");
        _mockBackend.Setup(b => b.Name).Returns("Local Backend");
        _mockBackend.Setup(b => b.Type).Returns(BackendType.Local);
        _mockBackend.Setup(b => b.IsHealthy).Returns(true);

        _mockRegistry.Setup(r => r.Get(It.IsAny<string?>())).Returns(_mockBackend.Object);
        _mockRegistry.Setup(r => r.GetAll()).Returns(new List<IBackend> { _mockBackend.Object });
        _mockRegistry.Setup(r => r.DefaultBackendId).Returns("local");

        _tools = new InstanceTools(_mockRegistry.Object, _mockHttpClientFactory.Object, _mockLogger.Object);
    }

    #region list_backends 测试

    [Fact]
    public void ListBackends_WithSingleBackend_ReturnsList()
    {
        // Act
        var result = _tools.ListBackends();

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Backends);
        Assert.Equal("local", result.Backends[0].Id);
        Assert.True(result.Backends[0].IsDefault);
    }

    [Fact]
    public void ListBackends_WithMultipleBackends_ReturnsAll()
    {
        // Arrange
        var remoteBackend = new Mock<IBackend>();
        remoteBackend.Setup(b => b.Id).Returns("remote1");
        remoteBackend.Setup(b => b.Name).Returns("Remote Backend");
        remoteBackend.Setup(b => b.Type).Returns(BackendType.Remote);
        remoteBackend.Setup(b => b.IsHealthy).Returns(true);

        _mockRegistry.Setup(r => r.GetAll()).Returns(new List<IBackend> { _mockBackend.Object, remoteBackend.Object });

        // Act
        var result = _tools.ListBackends();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Backends.Length);
    }

    [Fact]
    public void ListBackends_WithEmptyRegistry_ReturnsEmptyList()
    {
        // Arrange
        _mockRegistry.Setup(r => r.GetAll()).Returns(new List<IBackend>());

        // Act
        var result = _tools.ListBackends();

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Backends);
    }

    [Fact]
    public void ListBackends_IncludesHealthStatus()
    {
        // Arrange
        _mockBackend.Setup(b => b.IsHealthy).Returns(false);
        _mockBackend.Setup(b => b.LastHealthCheck).Returns(DateTime.UtcNow);

        // Act
        var result = _tools.ListBackends();

        // Assert
        Assert.False(result.Backends[0].IsHealthy);
        Assert.NotNull(result.Backends[0].LastHealthCheck);
    }

    #endregion

    #region unregister_backend 测试

    [Fact]
    public void UnregisterBackend_WithLocalBackend_ReturnsError()
    {
        // Act
        var result = _tools.UnregisterBackend("local");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Cannot unregister the local backend", result.Error);
    }

    [Fact]
    public void UnregisterBackend_WithExistingRemote_ReturnsSuccess()
    {
        // Arrange
        _mockRegistry.Setup(r => r.Unregister("remote1")).Returns(true);

        // Act
        var result = _tools.UnregisterBackend("remote1");

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.Error);
    }

    [Fact]
    public void UnregisterBackend_WithNonExistent_ReturnsError()
    {
        // Arrange
        _mockRegistry.Setup(r => r.Unregister("nonexistent")).Returns(false);

        // Act
        var result = _tools.UnregisterBackend("nonexistent");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Backend 'nonexistent' not found", result.Error);
    }

    #endregion

    #region set_default_backend 测试

    [Fact]
    public void SetDefaultBackend_WithExistingBackend_ReturnsSuccess()
    {
        // Arrange
        _mockRegistry.Setup(r => r.SetDefault("local")).Returns(true);

        // Act
        var result = _tools.SetDefaultBackend("local");

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.Error);
    }

    [Fact]
    public void SetDefaultBackend_WithNonExistent_ReturnsError()
    {
        // Arrange
        _mockRegistry.Setup(r => r.SetDefault("nonexistent")).Returns(false);

        // Act
        var result = _tools.SetDefaultBackend("nonexistent");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Backend 'nonexistent' not found", result.Error);
    }

    #endregion

    #region check_backend_health 测试

    [Fact]
    public async Task CheckBackendHealth_WithHealthyBackend_ReturnsHealthy()
    {
        // Arrange
        _mockBackend.Setup(b => b.CheckHealthAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockBackend.Setup(b => b.LastHealthCheck).Returns(DateTime.UtcNow);

        // Act
        var result = await _tools.CheckBackendHealth();

        // Assert
        Assert.True(result.Success);
        Assert.True(result.IsHealthy);
        Assert.Equal("local", result.BackendId);
    }

    [Fact]
    public async Task CheckBackendHealth_WithUnhealthyBackend_ReturnsUnhealthy()
    {
        // Arrange
        _mockBackend.Setup(b => b.CheckHealthAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var result = await _tools.CheckBackendHealth();

        // Assert
        Assert.True(result.Success);
        Assert.False(result.IsHealthy);
    }

    [Fact]
    public async Task CheckBackendHealth_WithNonExistentBackend_ReturnsError()
    {
        // Arrange
        _mockRegistry.Setup(r => r.Get("nonexistent")).Returns((IBackend?)null);

        // Act
        var result = await _tools.CheckBackendHealth("nonexistent");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Backend not found", result.Error);
    }

    [Fact]
    public async Task CheckBackendHealth_WithSpecificId_ChecksCorrectBackend()
    {
        // Arrange
        var remoteBackend = new Mock<IBackend>();
        remoteBackend.Setup(b => b.Id).Returns("remote1");
        remoteBackend.Setup(b => b.CheckHealthAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        _mockRegistry.Setup(r => r.Get("remote1")).Returns(remoteBackend.Object);

        // Act
        var result = await _tools.CheckBackendHealth("remote1");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("remote1", result.BackendId);
        remoteBackend.Verify(b => b.CheckHealthAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
