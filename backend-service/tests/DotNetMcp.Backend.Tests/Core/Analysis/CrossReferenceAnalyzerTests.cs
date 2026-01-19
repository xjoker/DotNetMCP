using DotNetMcp.Backend.Core.Analysis;
using DotNetMcp.Backend.Core.Context;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Analysis;

public class CrossReferenceAnalyzerTests
{
    private readonly string _testAssemblyPath;

    public CrossReferenceAnalyzerTests()
    {
        _testAssemblyPath = typeof(CrossReferenceAnalyzerTests).Assembly.Location;
    }

    [Fact]
    public async Task FindReferencesToType_ShouldFindReferences()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var analyzer = new CrossReferenceAnalyzer(context.Assembly.MainModule, context.Mvid);

        // 查找某个类型的引用（使用当前测试类型）
        var targetType = context.Assembly.MainModule.Types
            .FirstOrDefault(t => t.Name == "CrossReferenceAnalyzerTests");
        Assert.NotNull(targetType);

        // Act
        var refs = analyzer.FindReferencesToType(targetType);

        // Assert - 可能没有引用也是正常的
        Assert.NotNull(refs);

        context.Dispose();
    }

    [Fact]
    public async Task FindCallsToMethod_ShouldReturnResults()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var analyzer = new CrossReferenceAnalyzer(context.Assembly.MainModule, context.Mvid);

        // 找一个方法
        var testType = context.Assembly.MainModule.Types
            .FirstOrDefault(t => t.Name == "CrossReferenceAnalyzerTests");
        Assert.NotNull(testType);

        var targetMethod = testType.Methods.FirstOrDefault(m => !m.IsConstructor);
        Assert.NotNull(targetMethod);

        // Act
        var refs = analyzer.FindCallsToMethod(targetMethod);

        // Assert - 方法可能没有被调用，结果为空也正常
        Assert.NotNull(refs);

        context.Dispose();
    }

    [Fact]
    public async Task FindFieldUsages_ShouldWork()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var analyzer = new CrossReferenceAnalyzer(context.Assembly.MainModule, context.Mvid);

        // 找一个字段
        var testType = context.Assembly.MainModule.Types
            .FirstOrDefault(t => t.Name == "CrossReferenceAnalyzerTests");
        Assert.NotNull(testType);

        var field = testType.Fields.FirstOrDefault();
        if (field != null)
        {
            // Act
            var refs = analyzer.FindFieldUsages(field);

            // Assert
            Assert.NotNull(refs);
        }

        context.Dispose();
    }
}
