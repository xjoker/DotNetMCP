using DotNetMcp.Backend.Core.Analysis;
using DotNetMcp.Backend.Core.Context;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Analysis;

public class CallGraphAnalyzerTests
{
    private readonly string _testAssemblyPath;

    public CallGraphAnalyzerTests()
    {
        _testAssemblyPath = typeof(CallGraphAnalyzerTests).Assembly.Location;
    }

    [Fact]
    public async Task BuildCallGraph_ShouldCreateNodesAndEdges()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var analyzer = new CallGraphAnalyzer(context.Assembly.MainModule, context.Mvid);

        // Act
        var graph = analyzer.BuildCallGraph();

        // Assert
        Assert.True(graph.NodeCount > 0);
        Assert.True(graph.EdgeCount > 0);

        context.Dispose();
    }

    [Fact]
    public async Task GetCallees_ShouldReturnCalledMethods()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var analyzer = new CallGraphAnalyzer(context.Assembly.MainModule, context.Mvid);

        // 找一个有调用的方法
        var testType = context.Assembly.MainModule.Types
            .FirstOrDefault(t => t.Name == "CallGraphAnalyzerTests");
        Assert.NotNull(testType);

        var testMethod = testType.Methods
            .FirstOrDefault(m => m.Name == "BuildCallGraph_ShouldCreateNodesAndEdges");
        Assert.NotNull(testMethod);

        // Act
        var callees = analyzer.GetCallees(testMethod);

        // Assert
        Assert.NotEmpty(callees);

        context.Dispose();
    }

    [Fact]
    public async Task GetCallChain_ShouldReturnChainLevels()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var analyzer = new CallGraphAnalyzer(context.Assembly.MainModule, context.Mvid);

        var testType = context.Assembly.MainModule.Types
            .FirstOrDefault(t => t.Name == "CallGraphAnalyzerTests");
        Assert.NotNull(testType);

        var testMethod = testType.Methods
            .FirstOrDefault(m => m.Name == "BuildCallGraph_ShouldCreateNodesAndEdges");
        Assert.NotNull(testMethod);

        // Act
        var chain = analyzer.GetCallChain(testMethod, maxDepth: 3);

        // Assert
        Assert.NotNull(chain);
        Assert.NotEmpty(chain.StartMethodId);

        context.Dispose();
    }
}
