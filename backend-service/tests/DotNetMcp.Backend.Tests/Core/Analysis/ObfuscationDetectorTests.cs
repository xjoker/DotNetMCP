using DotNetMcp.Backend.Core.Analysis;
using DotNetMcp.Backend.Core.Context;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Analysis;

public class ObfuscationDetectorTests
{
    private readonly string _testAssemblyPath;

    public ObfuscationDetectorTests()
    {
        _testAssemblyPath = typeof(ObfuscationDetectorTests).Assembly.Location;
    }

    [Fact]
    public async Task Detect_NormalAssembly_ShouldReturnLowScore()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var detector = new ObfuscationDetector(context.Assembly.MainModule, context.Mvid);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.IsObfuscated);
        Assert.True(result.ObfuscationScore < 30, $"正常程序集得分应低于 30，实际得分: {result.ObfuscationScore}");

        context.Dispose();
    }

    [Fact]
    public async Task Detect_ShouldReturnStatistics()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var detector = new ObfuscationDetector(context.Assembly.MainModule, context.Mvid);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.NotNull(result.Statistics);
        Assert.True(result.Statistics.TotalTypes > 0, "应统计类型数量");
        Assert.True(result.Statistics.TotalMethods > 0, "应统计方法数量");
        Assert.True(result.Statistics.TotalFields >= 0, "应统计字段数量");

        context.Dispose();
    }

    [Fact]
    public async Task Detect_ShouldCheckObfuscatorMarkers()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var detector = new ObfuscationDetector(context.Assembly.MainModule, context.Mvid);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.NotNull(result.DetectedObfuscators);
        // 正常测试程序集不应检测到混淆器标记
        Assert.Empty(result.DetectedObfuscators);

        context.Dispose();
    }

    [Fact]
    public async Task Detect_ShouldAnalyzeIdentifiers()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var detector = new ObfuscationDetector(context.Assembly.MainModule, context.Mvid);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.NotNull(result.Statistics);
        // 正常程序集的非法标识符应该很少
        var invalidRatio = result.Statistics.TotalTypes > 0
            ? (double)result.Statistics.InvalidTypeNames / result.Statistics.TotalTypes
            : 0;
        Assert.True(invalidRatio < 0.1, $"非法标识符比例应低于 10%，实际: {invalidRatio:P1}");

        context.Dispose();
    }

    [Fact]
    public async Task CalculateScore_ShouldBeInRange()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var detector = new ObfuscationDetector(context.Assembly.MainModule, context.Mvid);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.True(result.ObfuscationScore >= 0, "混淆得分不应为负数");
        Assert.True(result.ObfuscationScore <= 100, "混淆得分不应超过 100");

        context.Dispose();
    }

    [Fact]
    public async Task Detect_ShouldReturnConfidenceLevel()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var detector = new ObfuscationDetector(context.Assembly.MainModule, context.Mvid);

        // Act
        var result = detector.Detect();

        // Assert
        Assert.NotNull(result.Confidence);
        Assert.Contains(result.Confidence, new[] { "Low", "Medium", "High" });

        context.Dispose();
    }
}
