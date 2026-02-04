using DotNetMcp.Backend.Core.Analysis;
using DotNetMcp.Backend.Core.Context;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Analysis;

public class DependencyGraphBuilderTests
{
    private readonly string _testAssemblyPath;

    public DependencyGraphBuilderTests()
    {
        _testAssemblyPath = typeof(DependencyGraphBuilderTests).Assembly.Location;
    }

    [Fact]
    public async Task BuildAssemblyDependencies_ShouldReturnRootNode()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var builder = new DependencyGraphBuilder(context.Assembly.MainModule, context.Mvid);

        // Act
        var graph = builder.BuildAssemblyDependencies();

        // Assert
        Assert.NotNull(graph);
        Assert.NotEmpty(graph.RootId);
        Assert.Equal(DependencyLevel.Assembly, graph.Level);
        Assert.True(graph.Nodes.ContainsKey(graph.RootId));
        Assert.False(graph.Nodes[graph.RootId].IsExternal);

        context.Dispose();
    }

    [Fact]
    public async Task BuildAssemblyDependencies_ShouldIncludeExternalReferences()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var builder = new DependencyGraphBuilder(context.Assembly.MainModule, context.Mvid);

        // Act
        var graph = builder.BuildAssemblyDependencies();

        // Assert
        Assert.True(graph.TotalNodes > 1, "应包含外部引用节点");
        Assert.True(graph.ExternalNodes > 0, "应存在外部程序集引用");
        Assert.True(graph.TotalEdges > 0, "应存在依赖边");

        // 验证所有外部节点都有对应的边
        var externalNodes = graph.Nodes.Values.Where(n => n.IsExternal).ToList();
        foreach (var extNode in externalNodes)
        {
            Assert.True(
                graph.Edges.Any(e => e.ToId == extNode.Id),
                $"外部节点 {extNode.Name} 应有入边");
        }

        context.Dispose();
    }

    [Fact]
    public async Task BuildNamespaceDependencies_ShouldGroupByNamespace()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var builder = new DependencyGraphBuilder(context.Assembly.MainModule, context.Mvid);

        // Act
        var graph = builder.BuildNamespaceDependencies();

        // Assert
        Assert.NotNull(graph);
        Assert.Equal(DependencyLevel.Namespace, graph.Level);
        Assert.True(graph.TotalNodes > 0, "应包含命名空间节点");

        // 验证所有节点类型都是 Namespace
        foreach (var node in graph.Nodes.Values)
        {
            Assert.Equal(DependencyNodeType.Namespace, node.Type);
        }

        // 验证包含测试项目的命名空间
        Assert.True(
            graph.Nodes.Keys.Any(k => k.Contains("DotNetMcp.Backend.Tests")),
            "应包含测试项目命名空间");

        context.Dispose();
    }

    [Fact]
    public async Task BuildTypeDependencies_ShouldAnalyzeTypeRelations()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var builder = new DependencyGraphBuilder(context.Assembly.MainModule, context.Mvid);

        // Act
        var graph = builder.BuildTypeDependencies();

        // Assert
        Assert.NotNull(graph);
        Assert.Equal(DependencyLevel.Type, graph.Level);
        Assert.True(graph.TotalNodes > 0, "应包含类型节点");

        // 验证节点类型
        foreach (var node in graph.Nodes.Values)
        {
            Assert.Equal(DependencyNodeType.Type, node.Type);
        }

        // 验证依赖边的种类
        var dependencyKinds = graph.Edges.Select(e => e.Kind).Distinct().ToList();
        Assert.NotEmpty(dependencyKinds);

        context.Dispose();
    }

    [Fact]
    public async Task ToMermaid_ShouldReturnValidSyntax()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();
        var builder = new DependencyGraphBuilder(context.Assembly.MainModule, context.Mvid);
        var graph = builder.BuildAssemblyDependencies();

        // Act
        var mermaid = builder.ToMermaid(graph);

        // Assert
        Assert.NotNull(mermaid);
        Assert.NotEmpty(mermaid);
        Assert.StartsWith("flowchart LR", mermaid);
        Assert.Contains("-->", mermaid);

        // 验证不包含非法字符（Mermaid ID 中的点号应被替换）
        var lines = mermaid.Split('\n');
        foreach (var line in lines.Where(l => l.Trim().StartsWith("_") || char.IsLetter(l.Trim().FirstOrDefault())))
        {
            // 节点定义行不应包含未转义的特殊字符
            if (line.Contains("[") || line.Contains("(("))
            {
                var idPart = line.Trim().Split('[')[0].Split('(')[0];
                Assert.DoesNotContain(".", idPart);
                Assert.DoesNotContain("<", idPart);
                Assert.DoesNotContain(">", idPart);
            }
        }

        context.Dispose();
    }
}
