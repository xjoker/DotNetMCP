using DotNetMcp.Server.Backend;
using DotNetMcp.Server.Tools;
using DotNetMcp.Backend.Services;
using DotNetMcp.Backend.Core.Context;

namespace DotNetMcp.Server.Tests.Tools;

/// <summary>
/// AssemblyTools 单元测试
/// </summary>
public class AssemblyToolsTests
{
    private readonly Mock<IBackendRegistry> _mockRegistry;
    private readonly Mock<IBackend> _mockBackend;
    private readonly AssemblyTools _tools;

    public AssemblyToolsTests()
    {
        _mockRegistry = new Mock<IBackendRegistry>();
        _mockBackend = new Mock<IBackend>();
        _mockBackend.Setup(b => b.Id).Returns("local");
        _mockRegistry.Setup(r => r.Get(It.IsAny<string?>())).Returns(_mockBackend.Object);
        _tools = new AssemblyTools(_mockRegistry.Object);
    }

    #region load_assembly 测试

    [Fact]
    public async Task LoadAssembly_WithValidPath_ReturnsSuccess()
    {
        // Arrange
        var testPath = "/path/to/test.dll";

        _mockBackend.Setup(b => b.LoadAssemblyAsync(testPath, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DotNetMcp.Backend.Core.Context.AssemblyLoadResult
            {
                IsSuccess = true
            });

        // Act
        var result = await _tools.LoadAssembly(testPath);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("local", result.Backend);
    }

    [Fact]
    public async Task LoadAssembly_WithInvalidPath_ReturnsError()
    {
        // Arrange
        var testPath = "/invalid/path.dll";
        _mockBackend.Setup(b => b.LoadAssemblyAsync(testPath, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DotNetMcp.Backend.Core.Context.AssemblyLoadResult
            {
                IsSuccess = false,
                ErrorMessage = "File not found"
            });

        // Act
        var result = await _tools.LoadAssembly(testPath);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("File not found", result.Error);
    }

    [Fact]
    public async Task LoadAssembly_WithNoBackend_ReturnsError()
    {
        // Arrange
        _mockRegistry.Setup(r => r.Get(It.IsAny<string?>())).Returns((IBackend?)null);
        var tools = new AssemblyTools(_mockRegistry.Object);

        // Act
        var result = await tools.LoadAssembly("/path/to/test.dll");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No backend available", result.Error);
    }

    [Fact]
    public async Task LoadAssembly_WithSearchPaths_PassesPathsToBackend()
    {
        // Arrange
        var testPath = "/path/to/test.dll";
        var searchPaths = new[] { "/lib1", "/lib2" };

        _mockBackend.Setup(b => b.LoadAssemblyAsync(testPath, searchPaths, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DotNetMcp.Backend.Core.Context.AssemblyLoadResult { IsSuccess = true });

        // Act
        await _tools.LoadAssembly(testPath, searchPaths);

        // Assert
        _mockBackend.Verify(b => b.LoadAssemblyAsync(testPath, searchPaths, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region list_assemblies 测试

    [Fact]
    public async Task ListAssemblies_WithNoAssemblies_ReturnsEmptyList()
    {
        // Arrange
        _mockBackend.Setup(b => b.ListAssembliesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DotNetMcp.Server.Backend.AssemblyListItem>());

        // Act
        var result = await _tools.ListAssemblies();

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Assemblies);
    }

    [Fact]
    public async Task ListAssemblies_WithMultipleAssemblies_ReturnsAllAssemblies()
    {
        // Arrange
        var assemblies = new List<DotNetMcp.Server.Backend.AssemblyListItem>
        {
            new() { Mvid = Guid.NewGuid().ToString(), Name = "Assembly1", Path = "/path1.dll", IsDefault = true },
            new() { Mvid = Guid.NewGuid().ToString(), Name = "Assembly2", Path = "/path2.dll", IsDefault = false }
        };

        _mockBackend.Setup(b => b.ListAssembliesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(assemblies);

        // Act
        var result = await _tools.ListAssemblies();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Assemblies.Length);
        Assert.Equal("Assembly1", result.Assemblies[0].Name);
        Assert.True(result.Assemblies[0].IsDefault);
    }

    [Fact]
    public async Task ListAssemblies_WithNoBackend_ReturnsError()
    {
        // Arrange
        _mockRegistry.Setup(r => r.Get(It.IsAny<string?>())).Returns((IBackend?)null);
        var tools = new AssemblyTools(_mockRegistry.Object);

        // Act
        var result = await tools.ListAssemblies();

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No backend available", result.Error);
    }

    #endregion

    #region unload_assembly 测试

    [Fact]
    public async Task UnloadAssembly_WithValidMvid_ReturnsSuccess()
    {
        // Arrange
        var mvid = Guid.NewGuid().ToString();
        _mockBackend.Setup(b => b.UnloadAssemblyAsync(mvid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _tools.UnloadAssembly(mvid);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task UnloadAssembly_WithInvalidMvid_ReturnsError()
    {
        // Arrange
        var mvid = "invalid-mvid";
        _mockBackend.Setup(b => b.UnloadAssemblyAsync(mvid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _tools.UnloadAssembly(mvid);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task UnloadAssembly_WithNoBackend_ReturnsError()
    {
        // Arrange
        _mockRegistry.Setup(r => r.Get(It.IsAny<string?>())).Returns((IBackend?)null);
        var tools = new AssemblyTools(_mockRegistry.Object);

        // Act
        var result = await tools.UnloadAssembly("any-mvid");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No backend available", result.Error);
    }

    #endregion
}
