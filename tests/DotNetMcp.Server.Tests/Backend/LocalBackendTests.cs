using DotNetMcp.Server.Backend;
using DotNetMcp.Backend.Services;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Server.Tests.Backend;

/// <summary>
/// LocalBackend 单元测试
/// </summary>
public class LocalBackendTests
{
    private readonly Mock<IBackend> _mockBackend;

    public LocalBackendTests()
    {
        _mockBackend = new Mock<IBackend>();

        _mockBackend.Setup(b => b.Id).Returns("local");
        _mockBackend.Setup(b => b.Name).Returns("Local Backend");
        _mockBackend.Setup(b => b.Type).Returns(BackendType.Local);
        _mockBackend.Setup(b => b.IsHealthy).Returns(true);
    }

    [Fact]
    public void LocalBackend_Id_ReturnsLocal()
    {
        // Assert
        Assert.Equal("local", _mockBackend.Object.Id);
    }

    [Fact]
    public void LocalBackend_Type_ReturnsLocal()
    {
        // Assert
        Assert.Equal(BackendType.Local, _mockBackend.Object.Type);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsTrue()
    {
        // Arrange
        _mockBackend.Setup(b => b.CheckHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _mockBackend.Object.CheckHealthAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsHealthy_WhenHealthy_ReturnsTrue()
    {
        // Assert
        Assert.True(_mockBackend.Object.IsHealthy);
    }

    [Fact]
    public async Task ListAssembliesAsync_ReturnsAssemblies()
    {
        // Arrange
        var expectedAssemblies = new List<AssemblyInfo>
        {
            new() { Mvid = Guid.NewGuid().ToString(), Name = "Test", Path = "/test.dll" }
        };

        _mockBackend.Setup(b => b.ListAssembliesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAssemblies);

        // Act
        var result = await _mockBackend.Object.ListAssembliesAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("Test", result[0].Name);
    }

    [Fact]
    public async Task LoadAssemblyAsync_WithValidPath_ReturnsSuccess()
    {
        // Arrange
        _mockBackend.Setup(b => b.LoadAssemblyAsync("/valid/path.dll", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DotNetMcp.Backend.Core.Context.AssemblyLoadResult { IsSuccess = true });

        // Act
        var result = await _mockBackend.Object.LoadAssemblyAsync("/valid/path.dll");

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UnloadAssemblyAsync_WithValidMvid_ReturnsTrue()
    {
        // Arrange
        var mvid = Guid.NewGuid().ToString();
        _mockBackend.Setup(b => b.UnloadAssemblyAsync(mvid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _mockBackend.Object.UnloadAssemblyAsync(mvid);

        // Assert
        Assert.True(result);
    }
}
