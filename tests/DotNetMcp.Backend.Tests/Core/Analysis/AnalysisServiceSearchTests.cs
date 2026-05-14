using DotNetMcp.Backend.Core.Context;
using DotNetMcp.Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Analysis;

/// <summary>
/// 验证 AnalysisService.SearchTypes / SearchStrings 使用 EnhancedSearchService 后
/// 支持正则模式和高级语法（+/-/= 前缀）
/// </summary>
public class AnalysisServiceSearchTests
{
    private readonly string _testAssemblyPath;
    private readonly AnalysisService _service;

    public AnalysisServiceSearchTests()
    {
        _testAssemblyPath = typeof(AnalysisServiceSearchTests).Assembly.Location;
        _service = new AnalysisService(NullLogger<AnalysisService>.Instance);
    }

    [Fact]
    public async Task SearchTypes_WithRegexPattern_ReturnsMatchingTypes()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();

        // Act：使用正则 /.*Tests$/ 匹配以 Tests 结尾的类型
        var result = _service.SearchTypes(context, "/.*Tests$/");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Types);
        Assert.All(result.Types, t => Assert.EndsWith("Tests", t.Name));

        context.Dispose();
    }

    [Fact]
    public async Task SearchTypes_WithAdvancedSyntaxExclude_FiltersCorrectly()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();

        // 先取所有含 "Service" 的类型数量
        var allServiceResult = _service.SearchTypes(context, "Service", limit: 200);

        // Act：+Service -Search 应返回含 Service 但不含 Search 的类型
        var filteredResult = _service.SearchTypes(context, "+Service -Search", limit: 200);

        // Assert
        Assert.True(filteredResult.IsSuccess);
        // 过滤后的结果不应包含名称中含 "Search" 的类型
        Assert.All(filteredResult.Types, t => Assert.DoesNotContain("Search", t.Name, StringComparison.OrdinalIgnoreCase));
        // 过滤结果数应 <= 未过滤数
        Assert.True(filteredResult.Types.Count <= allServiceResult.Types.Count);

        context.Dispose();
    }
}
