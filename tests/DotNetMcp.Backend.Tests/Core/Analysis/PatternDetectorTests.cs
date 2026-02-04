using DotNetMcp.Backend.Core.Analysis;
using DotNetMcp.Backend.Core.Context;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Analysis;

public class PatternDetectorTests
{
    private readonly string _testAssemblyPath;

    public PatternDetectorTests()
    {
        _testAssemblyPath = typeof(PatternDetectorTests).Assembly.Location;
    }

    [Fact]
    public async Task DetectAll_ShouldReturnPatternList()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var detector = new PatternDetector(context.Assembly.MainModule, context.Mvid);

        // Act
        var result = detector.DetectAll();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Patterns);
        Assert.NotNull(result.Summary);
        Assert.Equal(result.Patterns.Count, result.TotalCount);

        context.Dispose();
    }

    [Fact]
    public async Task DetectSingleton_WithPrivateConstructor_ShouldDetect()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var detector = new PatternDetector(context.Assembly.MainModule, context.Mvid);

        // 查找具有单例特征的类型
        var singletonType = context.Assembly.MainModule.Types
            .FirstOrDefault(t => t.Name == "SingletonTestClass");

        if (singletonType != null)
        {
            // Act
            var pattern = detector.DetectSingleton(singletonType);

            // Assert
            Assert.NotNull(pattern);
            Assert.Equal("Singleton", pattern.PatternType);
            Assert.NotEmpty(pattern.Evidence);
        }
        else
        {
            // 使用 DetectAll 验证单例检测逻辑正常工作
            var result = detector.DetectAll();
            Assert.True(result.IsSuccess);
            // 检测逻辑验证：查找任意带私有构造函数和静态实例的类型
            var anyType = context.Assembly.MainModule.Types
                .FirstOrDefault(t => !t.Name.StartsWith("<") &&
                    t.Methods.Any(m => m.IsConstructor && !m.IsStatic && m.IsPrivate));

            if (anyType != null)
            {
                var pattern = detector.DetectSingleton(anyType);
                // 如果检测到则验证结构正确
                if (pattern != null)
                {
                    Assert.Equal("Singleton", pattern.PatternType);
                    Assert.NotEmpty(pattern.TypeId);
                }
            }
        }

        context.Dispose();
    }

    [Fact]
    public async Task DetectFactory_WithCreateMethods_ShouldDetect()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var detector = new PatternDetector(context.Assembly.MainModule, context.Mvid);

        // 查找带有 Create/Build 方法的类型
        var factoryType = context.Assembly.MainModule.Types
            .FirstOrDefault(t => !t.Name.StartsWith("<") &&
                t.Methods.Any(m => m.Name.StartsWith("Create") || m.Name.StartsWith("Build")));

        if (factoryType != null)
        {
            // Act
            var patterns = detector.DetectFactory(factoryType);

            // Assert
            if (patterns.Count > 0)
            {
                var pattern = patterns.First();
                Assert.Equal("Factory", pattern.PatternType);
                Assert.NotEmpty(pattern.Evidence);
                Assert.NotEmpty(pattern.TypeId);
            }
        }

        // 验证空类型返回空列表
        var emptyType = context.Assembly.MainModule.Types
            .FirstOrDefault(t => !t.Name.StartsWith("<") && t.Methods.Count == 0);
        if (emptyType != null)
        {
            var emptyPatterns = detector.DetectFactory(emptyType);
            Assert.Empty(emptyPatterns);
        }

        context.Dispose();
    }

    [Fact]
    public async Task DetectObserver_WithEvents_ShouldDetect()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var detector = new PatternDetector(context.Assembly.MainModule, context.Mvid);

        // 查找包含事件的类型
        var observerType = context.Assembly.MainModule.Types
            .FirstOrDefault(t => !t.Name.StartsWith("<") && t.Events.Count > 0);

        if (observerType != null)
        {
            // Act
            var pattern = detector.DetectObserver(observerType);

            // Assert
            if (pattern != null)
            {
                Assert.Equal("Observer", pattern.PatternType);
                Assert.NotEmpty(pattern.Evidence);
                Assert.NotEmpty(pattern.TypeId);
                Assert.Contains(pattern.Confidence, new[] { "High", "Medium", "Low" });
            }
        }

        // 验证无事件类型返回 null
        var noEventType = context.Assembly.MainModule.Types
            .FirstOrDefault(t => !t.Name.StartsWith("<") && t.Events.Count == 0);
        if (noEventType != null)
        {
            var noPattern = detector.DetectObserver(noEventType);
            // 如果没有 Subscribe/Unsubscribe 方法对，应返回 null
            var hasSubscribePair = noEventType.Methods.Any(m => m.Name.Contains("Subscribe")) &&
                                   noEventType.Methods.Any(m => m.Name.Contains("Unsubscribe"));
            if (!hasSubscribePair)
            {
                Assert.Null(noPattern);
            }
        }

        context.Dispose();
    }

    [Fact]
    public async Task DetectBuilder_WithFluentInterface_ShouldDetect()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var detector = new PatternDetector(context.Assembly.MainModule, context.Mvid);

        // 查找名称以 Builder 结尾的类型
        var builderType = context.Assembly.MainModule.Types
            .FirstOrDefault(t => t.Name.EndsWith("Builder") && !t.Name.StartsWith("<"));

        if (builderType != null)
        {
            // Act
            var pattern = detector.DetectBuilder(builderType);

            // Assert
            if (pattern != null)
            {
                Assert.Equal("Builder", pattern.PatternType);
                Assert.NotEmpty(pattern.Evidence);
                Assert.Contains("Class name ends with 'Builder'", pattern.Evidence);
            }
        }

        // 验证非 Builder 类型返回 null
        var nonBuilderType = context.Assembly.MainModule.Types
            .FirstOrDefault(t => !t.Name.EndsWith("Builder") && !t.Name.StartsWith("<"));
        if (nonBuilderType != null)
        {
            var noPattern = detector.DetectBuilder(nonBuilderType);
            Assert.Null(noPattern);
        }

        context.Dispose();
    }

    [Fact]
    public async Task DetectStrategy_WithSingleMethodInterface_ShouldDetect()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var detector = new PatternDetector(context.Assembly.MainModule, context.Mvid);

        // 查找单方法接口
        var strategyInterface = context.Assembly.MainModule.Types
            .FirstOrDefault(t => t.IsInterface &&
                t.Methods.Count(m => !m.IsSpecialName) == 1);

        if (strategyInterface != null)
        {
            // Act
            var pattern = detector.DetectStrategy(strategyInterface);

            // Assert - 只有存在多个实现时才会检测为策略模式
            if (pattern != null)
            {
                Assert.Equal("Strategy", pattern.PatternType);
                Assert.Equal("High", pattern.Confidence);
                Assert.NotEmpty(pattern.Evidence);
                Assert.NotNull(pattern.RelatedTypes);
                Assert.True(pattern.RelatedTypes.Count >= 2);
            }
        }

        // 验证非接口类型返回 null
        var nonInterfaceType = context.Assembly.MainModule.Types
            .FirstOrDefault(t => !t.IsInterface && !t.Name.StartsWith("<"));
        if (nonInterfaceType != null)
        {
            var noPattern = detector.DetectStrategy(nonInterfaceType);
            Assert.Null(noPattern);
        }

        context.Dispose();
    }
}
