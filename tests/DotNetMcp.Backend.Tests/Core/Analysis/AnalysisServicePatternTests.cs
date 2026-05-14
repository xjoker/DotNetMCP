using DotNetMcp.Backend.Core.Context;
using DotNetMcp.Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Analysis;

/// <summary>
/// 验证 AnalysisService.DetectPatterns 端到端流程
/// </summary>
public class AnalysisServicePatternTests
{
    private readonly string _testAssemblyPath;
    private readonly AnalysisService _service;

    public AnalysisServicePatternTests()
    {
        _testAssemblyPath = typeof(AnalysisServicePatternTests).Assembly.Location;
        _service = new AnalysisService(NullLogger<AnalysisService>.Instance);
    }

    [Fact]
    public async Task DetectPatterns_WholeAssembly_ReturnsSuccessResult()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();

        // Act
        var result = _service.DetectPatterns(context);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Patterns);
        Assert.NotNull(result.Summary);
        Assert.Equal(result.TotalCount, result.Patterns.Count);
        // Summary 中的计数之和等于 TotalCount
        Assert.Equal(result.TotalCount, result.Summary.Values.Sum());

        context.Dispose();
    }

    [Fact]
    public async Task DetectPatterns_WithTypeName_OnlyAnalyzesThatType()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();

        // 找一个真实存在的 type
        var typeName = context.Assembly!.MainModule.Types
            .FirstOrDefault(t => !t.Name.StartsWith("<") && t.Methods.Count > 0)?.FullName;

        if (typeName == null)
        {
            context.Dispose();
            return; // 跳过：没有可用 type
        }

        // Act
        var result = _service.DetectPatterns(context, typeName);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Patterns);
        // 若检测到模式，TypeName 必须与指定的 type 匹配
        Assert.All(result.Patterns, p => Assert.Equal(typeName, p.TypeName));

        context.Dispose();
    }

    [Fact]
    public async Task DetectPatterns_WithNonExistentTypeName_ReturnsFailure()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();

        // Act
        var result = _service.DetectPatterns(context, "NonExistent.TypeThatDoesNotExist");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        context.Dispose();
    }
}
