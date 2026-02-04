using DotNetMcp.Server.Backend;
using DotNetMcp.Server.Tools;
using DotNetMcp.Backend.Services;

namespace DotNetMcp.Server.Tests.Tools;

/// <summary>
/// SearchTools 单元测试
/// </summary>
public class SearchToolsTests
{
    private readonly Mock<IBackendRegistry> _mockRegistry;
    private readonly Mock<IBackend> _mockBackend;
    private readonly SearchTools _tools;

    public SearchToolsTests()
    {
        _mockRegistry = new Mock<IBackendRegistry>();
        _mockBackend = new Mock<IBackend>();
        _mockBackend.Setup(b => b.Id).Returns("local");
        _mockRegistry.Setup(r => r.Get(It.IsAny<string?>())).Returns(_mockBackend.Object);
        _tools = new SearchTools(_mockRegistry.Object);
    }

    #region search_types 测试

    [Fact]
    public async Task SearchTypes_WithKeyword_ReturnsMatchingTypes()
    {
        // Arrange
        var keyword = "Test";
        var types = new List<TypeSummary>
        {
            new() { FullName = "MyNamespace.TestClass", Namespace = "MyNamespace", Name = "TestClass", Kind = "class", MethodCount = 5, FieldCount = 2 },
            new() { FullName = "MyNamespace.TestInterface", Namespace = "MyNamespace", Name = "TestInterface", Kind = "interface", MethodCount = 3, FieldCount = 0 }
        };

        _mockBackend.Setup(b => b.SearchTypesAsync("", keyword, null, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchTypesResult { IsSuccess = true, Types = types, TotalCount = 2 });

        // Act
        var result = await _tools.SearchTypes(keyword);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Types.Length);
        Assert.Equal("TestClass", result.Types[0].Name);
    }

    [Fact]
    public async Task SearchTypes_WithNamespaceFilter_FiltersCorrectly()
    {
        // Arrange
        var keyword = "Class";
        var namespaceFilter = "MyNamespace";

        _mockBackend.Setup(b => b.SearchTypesAsync("", keyword, namespaceFilter, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchTypesResult { IsSuccess = true, Types = new List<TypeSummary>(), TotalCount = 0 });

        // Act
        await _tools.SearchTypes(keyword, namespaceFilter: namespaceFilter);

        // Assert
        _mockBackend.Verify(b => b.SearchTypesAsync("", keyword, namespaceFilter, 50, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchTypes_WithLimit_RespectsLimit()
    {
        // Arrange
        var keyword = "Test";
        var limit = 10;

        _mockBackend.Setup(b => b.SearchTypesAsync("", keyword, null, limit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchTypesResult { IsSuccess = true, Types = new List<TypeSummary>(), TotalCount = 0 });

        // Act
        await _tools.SearchTypes(keyword, limit: limit);

        // Assert
        _mockBackend.Verify(b => b.SearchTypesAsync("", keyword, null, limit, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchTypes_WithNoBackend_ReturnsError()
    {
        // Arrange
        _mockRegistry.Setup(r => r.Get(It.IsAny<string?>())).Returns((IBackend?)null);
        var tools = new SearchTools(_mockRegistry.Object);

        // Act
        var result = await tools.SearchTypes("Test");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No backend available", result.Error);
    }

    [Fact]
    public async Task SearchTypes_WithNoMatches_ReturnsEmptyArray()
    {
        // Arrange
        _mockBackend.Setup(b => b.SearchTypesAsync("", "NonExistent", null, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchTypesResult { IsSuccess = true, Types = new List<TypeSummary>(), TotalCount = 0 });

        // Act
        var result = await _tools.SearchTypes("NonExistent");

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Types);
        Assert.Equal(0, result.TotalCount);
    }

    #endregion

    #region search_strings 测试

    [Fact]
    public async Task SearchStrings_WithContainsMode_ReturnsMatches()
    {
        // Arrange
        var query = "error";
        var matches = new List<StringMatch>
        {
            new() { Value = "An error occurred", TypeName = "Logger", MethodName = "LogError", ILOffset = 0x10 },
            new() { Value = "Error message", TypeName = "Handler", MethodName = "Handle", ILOffset = 0x20 }
        };

        _mockBackend.Setup(b => b.SearchStringsAsync("", query, "contains", 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchStringsResult { IsSuccess = true, Matches = matches, TotalCount = 2 });

        // Act
        var result = await _tools.SearchStrings(query);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Matches.Length);
        Assert.Contains("error", result.Matches[0].Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchStrings_WithExactMode_SearchesExactly()
    {
        // Arrange
        var query = "Hello World";

        _mockBackend.Setup(b => b.SearchStringsAsync("", query, "exact", 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchStringsResult { IsSuccess = true, Matches = new List<StringMatch>(), TotalCount = 0 });

        // Act
        await _tools.SearchStrings(query, mode: "exact");

        // Assert
        _mockBackend.Verify(b => b.SearchStringsAsync("", query, "exact", 50, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchStrings_WithStartsWithMode_SearchesPrefix()
    {
        // Arrange
        var query = "Error:";

        _mockBackend.Setup(b => b.SearchStringsAsync("", query, "startswith", 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchStringsResult { IsSuccess = true, Matches = new List<StringMatch>(), TotalCount = 0 });

        // Act
        await _tools.SearchStrings(query, mode: "startswith");

        // Assert
        _mockBackend.Verify(b => b.SearchStringsAsync("", query, "startswith", 50, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchStrings_WithNoBackend_ReturnsError()
    {
        // Arrange
        _mockRegistry.Setup(r => r.Get(It.IsAny<string?>())).Returns((IBackend?)null);
        var tools = new SearchTools(_mockRegistry.Object);

        // Act
        var result = await tools.SearchStrings("test");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No backend available", result.Error);
    }

    [Fact]
    public async Task SearchStrings_WithMvid_UsesSpecificAssembly()
    {
        // Arrange
        var mvid = Guid.NewGuid().ToString();
        var query = "test";

        _mockBackend.Setup(b => b.SearchStringsAsync(mvid, query, "contains", 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchStringsResult { IsSuccess = true, Matches = new List<StringMatch>(), TotalCount = 0 });

        // Act
        await _tools.SearchStrings(query, mvid: mvid);

        // Assert
        _mockBackend.Verify(b => b.SearchStringsAsync(mvid, query, "contains", 50, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
