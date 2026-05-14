using DotNetMcp.Backend.Core.Analysis;
using DotNetMcp.Backend.Core.Context;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Analysis;

public class DecompilerServiceTests
{
    private readonly string _testAssemblyPath;

    public DecompilerServiceTests()
    {
        _testAssemblyPath = typeof(DecompilerServiceTests).Assembly.Location;
    }

    [Fact]
    public async Task DecompileType_ShouldReturnCSharpCode()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var service = new DecompilerService(context);

        var testType = context.Assembly.MainModule.Types
            .FirstOrDefault(t => t.Name == "DecompilerServiceTests");
        Assert.NotNull(testType);

        // Act
        var result = service.DecompileType(testType.FullName);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Code);
        Assert.Contains("class", result.Code);

        context.Dispose();
    }

    [Fact]
    public async Task GetIL_ShouldReturnILCode()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var service = new DecompilerService(context);

        var testType = context.Assembly.MainModule.Types
            .FirstOrDefault(t => t.Name == "DecompilerServiceTests");
        Assert.NotNull(testType);

        // Act
        var il = service.GetIL(testType);

        // Assert
        Assert.NotNull(il);
        Assert.Contains(".class", il);
        Assert.Contains(".method", il);

        context.Dispose();
    }

    [Fact]
    public async Task DecompileMethod_ShouldReturnOnlyTargetMethod_NotEntireType()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        using var service = new DecompilerService(context);

        var testType = context.Assembly.MainModule.Types
            .FirstOrDefault(t => t.Name == "SampleClassForDecompileTest");
        Assert.NotNull(testType);

        var targetMethod = testType.Methods.FirstOrDefault(m => m.Name == "MethodA");
        Assert.NotNull(targetMethod);

        // Act
        var result = service.DecompileMethod(testType, targetMethod);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Code);
        // 结果应包含目标方法名
        Assert.Contains("MethodA", result.Code);
        // 结果不应包含同类中另一个方法的独有字符串（确认不是整个 type 的源码）
        Assert.DoesNotContain("MethodB_UniqueMarker", result.Code);
    }

    [Fact]
    public async Task DecompileMethod_Target_ShouldBeTypeAndMethodName()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        using var service = new DecompilerService(context);

        var testType = context.Assembly.MainModule.Types
            .FirstOrDefault(t => t.Name == "SampleClassForDecompileTest");
        Assert.NotNull(testType);

        var targetMethod = testType.Methods.FirstOrDefault(m => m.Name == "MethodA");
        Assert.NotNull(targetMethod);

        // Act
        var result = service.DecompileMethod(testType, targetMethod);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("SampleClassForDecompileTest", result.Target);
        Assert.Contains("MethodA", result.Target);
    }

    [Fact]
    public async Task DecompilerService_ShouldImplementIDisposable()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();

        // Act & Assert - should not throw
        using var service = new DecompilerService(context);
        service.DecompileType("DotNetMcp.Backend.Tests.Core.Analysis.SampleClassForDecompileTest");
        // Dispose is called implicitly by using; double-dispose should be safe
        service.Dispose();

        context.Dispose();
    }
}

/// <summary>
/// 用于反编译方法精确性测试的示例类
/// </summary>
public class SampleClassForDecompileTest
{
    public int MethodA(int x)
    {
        return x * 2;
    }

    public string MethodB_UniqueMarker(string input)
    {
        return "MethodB_UniqueMarker_" + input;
    }
}
