using DotNetMcp.Server.Backend;
using DotNetMcp.Server.Tools;
using DotNetMcp.Backend.Services;
using DotNetMcp.Backend.Core.Analysis;

namespace DotNetMcp.Server.Tests.Tools;

/// <summary>
/// AnalysisTools 单元测试
/// </summary>
public class AnalysisToolsTests
{
    private readonly Mock<IBackendRegistry> _mockRegistry;
    private readonly Mock<IBackend> _mockBackend;
    private readonly AnalysisTools _tools;

    public AnalysisToolsTests()
    {
        _mockRegistry = new Mock<IBackendRegistry>();
        _mockBackend = new Mock<IBackend>();
        _mockBackend.Setup(b => b.Id).Returns("local");
        _mockRegistry.Setup(r => r.Get(It.IsAny<string?>())).Returns(_mockBackend.Object);
        _tools = new AnalysisTools(_mockRegistry.Object);
    }

    #region decompile_type 测试

    [Fact]
    public async Task DecompileType_WithValidType_ReturnsCSharpCode()
    {
        // Arrange
        var typeName = "MyNamespace.MyClass";
        var expectedCode = "public class MyClass { }";

        _mockBackend.Setup(b => b.DecompileTypeAsync("", typeName, "csharp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(DecompileResult.Success(expectedCode, typeName));

        // Act
        var result = await _tools.DecompileType(typeName);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(typeName, result.TypeName);
        Assert.Equal(expectedCode, result.Code);
    }

    [Fact]
    public async Task DecompileType_WithILLanguage_ReturnsILCode()
    {
        // Arrange
        var typeName = "MyNamespace.MyClass";
        var expectedIL = ".class public MyClass { }";

        _mockBackend.Setup(b => b.DecompileTypeAsync("", typeName, "il", It.IsAny<CancellationToken>()))
            .ReturnsAsync(DecompileResult.Success(expectedIL, typeName));

        // Act
        var result = await _tools.DecompileType(typeName, language: "il");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expectedIL, result.Code);
    }

    [Fact]
    public async Task DecompileType_WithNonExistentType_ReturnsError()
    {
        // Arrange
        var typeName = "NonExistent.Type";

        _mockBackend.Setup(b => b.DecompileTypeAsync("", typeName, "csharp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(DecompileResult.Failure("Type not found"));

        // Act
        var result = await _tools.DecompileType(typeName);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Type not found", result.Error);
    }

    [Fact]
    public async Task DecompileType_WithNoBackend_ReturnsError()
    {
        // Arrange
        _mockRegistry.Setup(r => r.Get(It.IsAny<string?>())).Returns((IBackend?)null);
        var tools = new AnalysisTools(_mockRegistry.Object);

        // Act
        var result = await tools.DecompileType("Any.Type");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No backend available", result.Error);
    }

    #endregion

    #region decompile_method 测试

    [Fact]
    public async Task DecompileMethod_WithValidMethod_ReturnsCode()
    {
        // Arrange
        var typeName = "MyNamespace.MyClass";
        var methodName = "MyMethod";
        var expectedCode = "public void MyMethod() { }";

        _mockBackend.Setup(b => b.DecompileMethodAsync("", typeName, methodName, "csharp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(DecompileResult.Success(expectedCode, typeName));

        // Act
        var result = await _tools.DecompileMethod(typeName, methodName);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(methodName, result.MethodName);
        Assert.Equal(expectedCode, result.Code);
    }

    [Fact]
    public async Task DecompileMethod_WithNoBackend_ReturnsError()
    {
        // Arrange
        _mockRegistry.Setup(r => r.Get(It.IsAny<string?>())).Returns((IBackend?)null);
        var tools = new AnalysisTools(_mockRegistry.Object);

        // Act
        var result = await tools.DecompileMethod("Any.Type", "AnyMethod");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No backend available", result.Error);
    }

    #endregion

    #region find_type_references 测试

    [Fact]
    public async Task FindTypeReferences_WithReferences_ReturnsAll()
    {
        // Arrange
        var typeName = "MyNamespace.MyClass";
        var refs = new List<CrossReference>
        {
            new() { SourceTypeId = "id1", SourceTypeName = "OtherClass", SourceMemberName = "Method1", TargetName = typeName, Kind = ReferenceKind.TypeReference },
            new() { SourceTypeId = "id2", SourceTypeName = "AnotherClass", SourceMemberName = "Method2", TargetName = typeName, Kind = ReferenceKind.TypeReference }
        };

        _mockBackend.Setup(b => b.FindReferencesToTypeAsync("", typeName, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new XRefResult { IsSuccess = true, References = refs, TotalCount = 2 });

        // Act
        var result = await _tools.FindTypeReferences(typeName);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.References.Length);
    }

    [Fact]
    public async Task FindTypeReferences_WithNoReferences_ReturnsEmpty()
    {
        // Arrange
        var typeName = "Unused.Type";

        _mockBackend.Setup(b => b.FindReferencesToTypeAsync("", typeName, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new XRefResult { IsSuccess = true, References = new List<CrossReference>(), TotalCount = 0 });

        // Act
        var result = await _tools.FindTypeReferences(typeName);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.References);
    }

    [Fact]
    public async Task FindTypeReferences_WithNoBackend_ReturnsError()
    {
        // Arrange
        _mockRegistry.Setup(r => r.Get(It.IsAny<string?>())).Returns((IBackend?)null);
        var tools = new AnalysisTools(_mockRegistry.Object);

        // Act
        var result = await tools.FindTypeReferences("Any.Type");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No backend available", result.Error);
    }

    #endregion

    #region find_method_calls 测试

    [Fact]
    public async Task FindMethodCalls_WithCalls_ReturnsAll()
    {
        // Arrange
        var typeName = "MyNamespace.MyClass";
        var methodName = "MyMethod";
        var refs = new List<CrossReference>
        {
            new() { SourceTypeId = "id1", SourceTypeName = "Caller1", SourceMemberName = "CallMethod", TargetName = methodName, Kind = ReferenceKind.MethodCall }
        };

        _mockBackend.Setup(b => b.FindCallsToMethodAsync("", typeName, methodName, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new XRefResult { IsSuccess = true, References = refs, TotalCount = 1 });

        // Act
        var result = await _tools.FindMethodCalls(typeName, methodName);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.References);
    }

    #endregion

    #region get_call_graph 测试

    [Fact]
    public async Task GetCallGraph_WithCallees_ReturnsGraph()
    {
        // Arrange
        var typeName = "MyNamespace.MyClass";
        var methodName = "EntryPoint";
        var levels = new List<CallGraphLevel>
        {
            new() { Depth = 1, Methods = new List<string> { "Method1", "Method2" } },
            new() { Depth = 2, Methods = new List<string> { "Method3" } }
        };

        _mockBackend.Setup(b => b.BuildCallGraphAsync("", typeName, methodName, "callees", 3, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallGraphResult { IsSuccess = true, StartMethod = $"{typeName}.{methodName}", Levels = levels, MaxDepthReached = false });

        // Act
        var result = await _tools.GetCallGraph(typeName, methodName);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Levels);
        Assert.Equal(2, result.Levels.Length);
        Assert.False(result.MaxDepthReached);
    }

    [Fact]
    public async Task GetCallGraph_WithNoBackend_ReturnsError()
    {
        // Arrange
        _mockRegistry.Setup(r => r.Get(It.IsAny<string?>())).Returns((IBackend?)null);
        var tools = new AnalysisTools(_mockRegistry.Object);

        // Act
        var result = await tools.GetCallGraph("Any.Type", "AnyMethod");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No backend available", result.Error);
    }

    #endregion

    #region get_control_flow_graph 测试

    [Fact]
    public async Task GetControlFlowGraph_WithValidMethod_ReturnsCFG()
    {
        // Arrange
        var typeName = "MyNamespace.MyClass";
        var methodName = "ComplexMethod";
        var mermaid = "graph TD\n  BB0 --> BB1\n  BB1 --> BB2";

        _mockBackend.Setup(b => b.BuildControlFlowGraphAsync("", typeName, methodName, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CFGResult { IsSuccess = true, MethodName = methodName, BlockCount = 3, EdgeCount = 2, Mermaid = mermaid });

        // Act
        var result = await _tools.GetControlFlowGraph(typeName, methodName);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(methodName, result.MethodName);
        Assert.Equal(3, result.BlockCount);
        Assert.Equal(2, result.EdgeCount);
        Assert.Contains("graph TD", result.Mermaid);
    }

    [Fact]
    public async Task GetControlFlowGraph_WithNoBackend_ReturnsError()
    {
        // Arrange
        _mockRegistry.Setup(r => r.Get(It.IsAny<string?>())).Returns((IBackend?)null);
        var tools = new AnalysisTools(_mockRegistry.Object);

        // Act
        var result = await tools.GetControlFlowGraph("Any.Type", "AnyMethod");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No backend available", result.Error);
    }

    #endregion
}
