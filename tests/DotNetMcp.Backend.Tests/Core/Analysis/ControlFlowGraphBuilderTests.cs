using DotNetMcp.Backend.Core.Analysis;
using DotNetMcp.Backend.Core.Context;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Analysis;

public class ControlFlowGraphBuilderTests
{
    private readonly string _testAssemblyPath;

    public ControlFlowGraphBuilderTests()
    {
        _testAssemblyPath = typeof(ControlFlowGraphBuilderTests).Assembly.Location;
    }

    [Fact]
    public async Task Build_SimpleMethod_ShouldReturnSingleBlock()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var builder = new ControlFlowGraphBuilder(context.Assembly.MainModule, context.Mvid);

        // 查找一个简单的 getter 方法（通常只有单个基本块）
        var testType = context.Assembly.MainModule.Types
            .FirstOrDefault(t => t.Name == "ControlFlowGraphBuilderTests");
        Assert.NotNull(testType);

        var simpleMethod = testType.Methods
            .FirstOrDefault(m => m.IsConstructor && !m.IsStatic);
        Assert.NotNull(simpleMethod);

        // Act
        var cfg = builder.Build(simpleMethod);

        // Assert
        Assert.NotNull(cfg);
        Assert.Single(cfg.Blocks);
        Assert.Equal(BlockType.Entry, cfg.Blocks[0].Type);

        context.Dispose();
    }

    [Fact]
    public async Task Build_MethodWithBranch_ShouldReturnMultipleBlocks()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var builder = new ControlFlowGraphBuilder(context.Assembly.MainModule, context.Mvid);

        // 查找带有分支逻辑的方法 - 在测试程序集中找一个有分支的方法
        var branchMethod = context.Assembly.MainModule.Types
            .SelectMany(t => t.Methods)
            .FirstOrDefault(m => m.HasBody && m.Body.Instructions.Count > 10 &&
                m.Body.Instructions.Any(i => i.OpCode.FlowControl == Mono.Cecil.Cil.FlowControl.Cond_Branch));
        Assert.NotNull(branchMethod);

        // Act
        var cfg = builder.Build(branchMethod);

        // Assert
        Assert.NotNull(cfg);
        Assert.True(cfg.Blocks.Count > 1, "分支方法应该产生多个基本块");
        Assert.True(cfg.Edges.Count > 0, "分支方法应该有控制流边");

        context.Dispose();
    }

    [Fact]
    public async Task Build_MethodWithoutBody_ShouldReturnEmptyGraph()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var builder = new ControlFlowGraphBuilder(context.Assembly.MainModule, context.Mvid);

        // 查找抽象方法或接口方法（无方法体）
        var abstractMethod = context.Assembly.MainModule.Types
            .SelectMany(t => t.Methods)
            .FirstOrDefault(m => !m.HasBody);

        if (abstractMethod == null)
        {
            // 如果没有无方法体的方法，跳过测试
            return;
        }

        // Act
        var cfg = builder.Build(abstractMethod);

        // Assert
        Assert.NotNull(cfg);
        Assert.Empty(cfg.Blocks);
        Assert.Empty(cfg.Edges);
        Assert.Null(cfg.EntryBlockId);
        Assert.Empty(cfg.ExitBlockIds);

        context.Dispose();
    }

    [Fact]
    public async Task ToMermaid_ShouldReturnValidMermaidSyntax()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var builder = new ControlFlowGraphBuilder(context.Assembly.MainModule, context.Mvid);

        var testType = context.Assembly.MainModule.Types
            .FirstOrDefault(t => t.Name == "ControlFlowGraphBuilderTests");
        Assert.NotNull(testType);

        var testMethod = testType.Methods
            .FirstOrDefault(m => m.Name == "Build_SimpleMethod_ShouldReturnSingleBlock");
        Assert.NotNull(testMethod);

        var cfg = builder.Build(testMethod);

        // Act
        var mermaid = builder.ToMermaid(cfg);

        // Assert
        Assert.NotNull(mermaid);
        Assert.Contains("flowchart TD", mermaid);
        Assert.Contains("BB0", mermaid);

        context.Dispose();
    }

    [Fact]
    public async Task Build_ShouldIdentifyEntryAndExitBlocks()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var builder = new ControlFlowGraphBuilder(context.Assembly.MainModule, context.Mvid);

        var testType = context.Assembly.MainModule.Types
            .FirstOrDefault(t => t.Name == "ControlFlowGraphBuilderTests");
        Assert.NotNull(testType);

        var testMethod = testType.Methods
            .FirstOrDefault(m => m.Name == "Build_SimpleMethod_ShouldReturnSingleBlock" && m.HasBody);
        Assert.NotNull(testMethod);

        // Act
        var cfg = builder.Build(testMethod);

        // Assert
        Assert.NotNull(cfg);
        Assert.NotNull(cfg.EntryBlockId);
        Assert.NotEmpty(cfg.ExitBlockIds);

        // 入口块应该是第一个块
        Assert.Equal(cfg.Blocks.First().Id, cfg.EntryBlockId);

        // 入口块类型应该是 Entry
        var entryBlock = cfg.Blocks.FirstOrDefault(b => b.Id == cfg.EntryBlockId);
        Assert.NotNull(entryBlock);
        Assert.Equal(BlockType.Entry, entryBlock.Type);

        context.Dispose();
    }
}
