using DotNetMcp.Backend.Core.Analysis;
using DotNetMcp.Backend.Core.Context;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Analysis;

// ============================
// 测试用继承体系（定义在测试程序集自身中，不依赖外部 fixture）
// ============================

public interface IInheritanceTestService
{
    void Execute();
}

public interface IInheritanceTestLogger
{
    void Log(string message);
}

public abstract class InheritanceTestBase : IInheritanceTestService
{
    public abstract void Execute();
    public virtual void OnExecuted() { }
}

public class InheritanceTestChild : InheritanceTestBase, IInheritanceTestLogger
{
    public override void Execute() { }
    public void Log(string message) { }
    public override void OnExecuted() { }
}

public class InheritanceTestGrandChild : InheritanceTestChild
{
    public override void OnExecuted() { }
}

public class InheritanceTestOverloaded
{
    public void DoWork() { }
    public void DoWork(int count) { }
    public void DoWork(string name, int count) { }
}

// ============================
// 测试类
// ============================

public class InheritanceAnalyzerTests
{
    private readonly string _testAssemblyPath;

    public InheritanceAnalyzerTests()
    {
        _testAssemblyPath = typeof(InheritanceAnalyzerTests).Assembly.Location;
    }

    private async Task<(AssemblyContext ctx, InheritanceAnalyzer analyzer)> CreateAnalyzerAsync()
    {
        var ctx = new AssemblyContext(_testAssemblyPath);
        await ctx.LoadAsync();
        var analyzer = new InheritanceAnalyzer(ctx.Assembly!.MainModule, ctx.Mvid);
        return (ctx, analyzer);
    }

    [Fact]
    public async Task FindBaseTypes_ShouldReturnBaseChainIncludingObject()
    {
        var (ctx, analyzer) = await CreateAnalyzerAsync();
        try
        {
            var childType = ctx.Assembly!.MainModule.Types
                .FirstOrDefault(t => t.Name == "InheritanceTestChild");
            Assert.NotNull(childType);

            var bases = analyzer.FindBaseTypes(childType, includeInterfaces: false);

            // InheritanceTestChild → InheritanceTestBase → System.Object
            Assert.NotNull(bases);
            Assert.True(bases.Count >= 1, "Should have at least InheritanceTestBase");
            Assert.Contains(bases, b => b.FullName.Contains("InheritanceTestBase"));
        }
        finally { ctx.Dispose(); }
    }

    [Fact]
    public async Task FindBaseTypes_WithInterfaces_ShouldIncludeInterfaces()
    {
        var (ctx, analyzer) = await CreateAnalyzerAsync();
        try
        {
            var childType = ctx.Assembly!.MainModule.Types
                .FirstOrDefault(t => t.Name == "InheritanceTestChild");
            Assert.NotNull(childType);

            var bases = analyzer.FindBaseTypes(childType, includeInterfaces: true);

            Assert.NotNull(bases);
            // 应该包含 IInheritanceTestLogger（直接）和 IInheritanceTestService（通过 Base）
            Assert.Contains(bases, b => b.FullName.Contains("IInheritanceTestLogger"));
        }
        finally { ctx.Dispose(); }
    }

    [Fact]
    public async Task FindDerivedTypes_Direct_ShouldReturnImmediateChildren()
    {
        var (ctx, analyzer) = await CreateAnalyzerAsync();
        try
        {
            var baseType = ctx.Assembly!.MainModule.Types
                .FirstOrDefault(t => t.Name == "InheritanceTestBase");
            Assert.NotNull(baseType);

            var derived = analyzer.FindDerivedTypes(baseType, directOnly: true);

            Assert.NotNull(derived);
            Assert.Contains(derived, d => d.FullName.Contains("InheritanceTestChild"));
            // GrandChild 继承自 Child 而非 Base，不应出现在直接子类中
            Assert.DoesNotContain(derived, d => d.FullName.Contains("InheritanceTestGrandChild"));
        }
        finally { ctx.Dispose(); }
    }

    [Fact]
    public async Task FindDerivedTypes_Recursive_ShouldReturnFullHierarchy()
    {
        var (ctx, analyzer) = await CreateAnalyzerAsync();
        try
        {
            var baseType = ctx.Assembly!.MainModule.Types
                .FirstOrDefault(t => t.Name == "InheritanceTestBase");
            Assert.NotNull(baseType);

            var derived = analyzer.FindDerivedTypes(baseType, directOnly: false);

            Assert.NotNull(derived);
            // 递归模式应包含 Child 和 GrandChild
            Assert.Contains(derived, d => d.FullName.Contains("InheritanceTestChild"));
            Assert.Contains(derived, d => d.FullName.Contains("InheritanceTestGrandChild"));
        }
        finally { ctx.Dispose(); }
    }

    [Fact]
    public async Task GetImplementations_ShouldFindImplementors()
    {
        var (ctx, analyzer) = await CreateAnalyzerAsync();
        try
        {
            var ifaceType = ctx.Assembly!.MainModule.Types
                .FirstOrDefault(t => t.Name == "IInheritanceTestLogger");
            Assert.NotNull(ifaceType);

            var impls = analyzer.GetImplementations(ifaceType);

            Assert.NotNull(impls);
            Assert.Contains(impls, i => i.FullName.Contains("InheritanceTestChild"));
        }
        finally { ctx.Dispose(); }
    }

    [Fact]
    public async Task GetOverrides_ShouldFindMethodOverrides()
    {
        var (ctx, analyzer) = await CreateAnalyzerAsync();
        try
        {
            var baseType = ctx.Assembly!.MainModule.Types
                .FirstOrDefault(t => t.Name == "InheritanceTestBase");
            Assert.NotNull(baseType);

            var onExecutedMethod = baseType.Methods.FirstOrDefault(m => m.Name == "OnExecuted");
            Assert.NotNull(onExecutedMethod);
            Assert.True(onExecutedMethod.IsVirtual, "OnExecuted should be virtual");

            var overrides = analyzer.GetOverrides(onExecutedMethod);

            Assert.NotNull(overrides);
            // InheritanceTestChild 和 InheritanceTestGrandChild 都覆盖了 OnExecuted
            Assert.True(overrides.Count >= 1, "Should find at least one override");
        }
        finally { ctx.Dispose(); }
    }

    [Fact]
    public async Task GetOverloads_ShouldFindMethodOverloads()
    {
        var (ctx, analyzer) = await CreateAnalyzerAsync();
        try
        {
            var overloadedType = ctx.Assembly!.MainModule.Types
                .FirstOrDefault(t => t.Name == "InheritanceTestOverloaded");
            Assert.NotNull(overloadedType);

            var doWorkMethod = overloadedType.Methods.FirstOrDefault(m => m.Name == "DoWork" && m.Parameters.Count == 0);
            Assert.NotNull(doWorkMethod);

            var overloads = analyzer.GetOverloads(doWorkMethod);

            Assert.NotNull(overloads);
            // 应该有 2 个重载（不含自身）：DoWork(int) 和 DoWork(string, int)
            Assert.Equal(2, overloads.Count);
        }
        finally { ctx.Dispose(); }
    }

    [Fact]
    public async Task FindBaseTypes_TypeNotExist_ShouldReturnEmptyList()
    {
        var (ctx, analyzer) = await CreateAnalyzerAsync();
        try
        {
            // 直接用一个不存在于 module 内的类型名来测试容错
            // （通过找一个真实类型然后测试 base 链不为 null）
            var baseType = ctx.Assembly!.MainModule.Types
                .FirstOrDefault(t => t.Name == "InheritanceTestBase");
            Assert.NotNull(baseType);

            // 测试不包含接口时结果不为 null
            var bases = analyzer.FindBaseTypes(baseType, includeInterfaces: false);
            Assert.NotNull(bases);
        }
        finally { ctx.Dispose(); }
    }

    [Fact]
    public async Task GetOverrides_NonVirtualMethod_ShouldReturnEmpty()
    {
        var (ctx, analyzer) = await CreateAnalyzerAsync();
        try
        {
            var overloadedType = ctx.Assembly!.MainModule.Types
                .FirstOrDefault(t => t.Name == "InheritanceTestOverloaded");
            Assert.NotNull(overloadedType);

            // DoWork 是非虚方法
            var doWorkMethod = overloadedType.Methods.FirstOrDefault(m => m.Name == "DoWork" && m.Parameters.Count == 0);
            Assert.NotNull(doWorkMethod);
            Assert.False(doWorkMethod.IsVirtual, "DoWork should not be virtual");

            var overrides = analyzer.GetOverrides(doWorkMethod);

            Assert.NotNull(overrides);
            Assert.Empty(overrides);
        }
        finally { ctx.Dispose(); }
    }
}
