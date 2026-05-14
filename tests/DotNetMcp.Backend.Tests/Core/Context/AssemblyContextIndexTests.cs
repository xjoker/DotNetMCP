using System.Diagnostics;
using DotNetMcp.Backend.Core.Context;
using DotNetMcp.Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Context;

public class AssemblyContextIndexTests
{
    private static string GetFixtureAssemblyPath()
    {
        // 使用当前测试程序集本身作为 fixture
        return typeof(AssemblyContextIndexTests).Assembly.Location;
    }

    private static async Task<AssemblyContext> LoadContextAsync()
    {
        var path = GetFixtureAssemblyPath();
        var context = new AssemblyContext(path);
        var result = await context.LoadAsync();
        Assert.True(result.IsSuccess, "Assembly load should succeed");
        return context;
    }

    [Fact]
    public async Task TypeIndex_BeforeAccess_IsNotBuilt()
    {
        // Arrange
        var context = await LoadContextAsync();

        // Assert: Lazy 在访问 .Value 前不应自动构建
        Assert.False(context.IsTypeIndexBuilt);
        Assert.False(context.IsMemberIndexBuilt);
    }

    [Fact]
    public async Task TypeIndex_AfterAccess_IsBuilt()
    {
        // Arrange
        var context = await LoadContextAsync();
        Assert.False(context.IsTypeIndexBuilt);

        // Act: 访问 TypeIndex 触发 Lazy 构建
        var ti = context.TypeIndex;

        // Assert
        Assert.True(context.IsTypeIndexBuilt);
        Assert.NotNull(ti);
    }

    [Fact]
    public async Task MemberIndex_AfterAccess_IsBuilt()
    {
        // Arrange
        var context = await LoadContextAsync();
        Assert.False(context.IsMemberIndexBuilt);

        // Act
        var mi = context.MemberIndex;

        // Assert
        Assert.True(context.IsMemberIndexBuilt);
        Assert.NotNull(mi);
    }

    [Fact]
    public async Task TypeIndex_AccessedTwice_ReturnsSameInstance()
    {
        // Arrange
        var context = await LoadContextAsync();

        // Act
        var ti1 = context.TypeIndex;
        var ti2 = context.TypeIndex;

        // Assert: Lazy 缓存——返回同一对象
        Assert.Same(ti1, ti2);
    }

    [Fact]
    public async Task TypeIndex_AccessedTwice_BuildsOnlyOnce()
    {
        // Arrange
        var context = await LoadContextAsync();

        // Act: 两次访问，测量总耗时
        var sw = Stopwatch.StartNew();
        _ = context.TypeIndex;
        sw.Stop();
        var firstAccessMs = sw.ElapsedMilliseconds;

        sw.Restart();
        _ = context.TypeIndex;
        sw.Stop();
        var secondAccessMs = sw.ElapsedMilliseconds;

        // Assert: 第二次访问应该极快（< 10ms），远快于首次构建
        Assert.True(secondAccessMs < Math.Max(firstAccessMs / 2, 50),
            $"Second access ({secondAccessMs}ms) should be much faster than first ({firstAccessMs}ms)");
    }

    [Fact]
    public async Task TypeIndex_Count_IsGreaterThanZero()
    {
        // Arrange
        var context = await LoadContextAsync();

        // Act
        var ti = context.TypeIndex;

        // Assert: 测试程序集本身包含至少一个类型
        Assert.True(ti.Count > 0, $"TypeIndex should contain types, got {ti.Count}");
    }

    [Fact]
    public async Task MemberIndex_Count_IsGreaterThanZero()
    {
        // Arrange
        var context = await LoadContextAsync();

        // Act
        var mi = context.MemberIndex;

        // Assert
        Assert.True(mi.Count > 0, $"MemberIndex should contain members, got {mi.Count}");
    }

    [Fact]
    public async Task WarmIndex_ReturnsTypeCount_GreaterThanZero()
    {
        // Arrange
        var context = await LoadContextAsync();
        var service = new AnalysisService(NullLogger<AnalysisService>.Instance);

        // Act
        var result = service.WarmIndex(context, typeIndex: true, memberIndex: false);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.TypeIndexBuilt);
        Assert.False(result.MemberIndexBuilt);
        Assert.True(result.TypeCount > 0, $"WarmIndex TypeCount should be > 0, got {result.TypeCount}");
    }

    [Fact]
    public async Task WarmIndex_BothIndexes_ReturnsCorrectCounts()
    {
        // Arrange
        var context = await LoadContextAsync();
        var service = new AnalysisService(NullLogger<AnalysisService>.Instance);

        // Act
        var result = service.WarmIndex(context, typeIndex: true, memberIndex: true);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.TypeIndexBuilt);
        Assert.True(result.MemberIndexBuilt);
        Assert.True(result.TypeCount > 0);
        Assert.True(result.MemberCount > 0);
        Assert.True(result.ElapsedMs >= 0);
    }

    [Fact]
    public async Task WarmIndex_WithMaxSeconds_SkipsMemberIndexIfExceeded()
    {
        // Arrange: 用 0 秒上限确保触发超时跳过
        var context = await LoadContextAsync();
        var service = new AnalysisService(NullLogger<AnalysisService>.Instance);

        // Act: 先预热 TypeIndex（计时器已超 0s），memberIndex 会被跳过
        var result = service.WarmIndex(context, typeIndex: true, memberIndex: true, maxSeconds: 0);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.TypeIndexBuilt);
        // 超时后 MemberIndex 不应被构建
        Assert.False(result.MemberIndexBuilt);
        Assert.True(result.MaxSecondsExceeded);
    }

    [Fact]
    public async Task Reload_ResetsLazyIndex()
    {
        // Arrange
        var context = await LoadContextAsync();

        // 先触发构建
        _ = context.TypeIndex;
        Assert.True(context.IsTypeIndexBuilt);

        // Act: Reload 重置 Lazy 状态
        context.Reload(context.Assembly);

        // Assert: 重置后索引标志应为 false
        Assert.False(context.IsTypeIndexBuilt);
        Assert.False(context.IsMemberIndexBuilt);
    }
}
