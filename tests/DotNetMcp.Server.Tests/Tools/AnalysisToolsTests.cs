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

        _mockBackend.Setup(b => b.DecompileTypeAsync("", typeName, "csharp", false, It.IsAny<CancellationToken>()))
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

        _mockBackend.Setup(b => b.DecompileTypeAsync("", typeName, "il", false, It.IsAny<CancellationToken>()))
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

        _mockBackend.Setup(b => b.DecompileTypeAsync("", typeName, "csharp", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DecompileResult.Failure("Type not found"));

        // Act
        var result = await _tools.DecompileType(typeName);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Type not found", result.Error);
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
        Assert.Contains("No backend available", result.Error);
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

        _mockBackend.Setup(b => b.DecompileMethodAsync("", typeName, methodName, "csharp", false, It.IsAny<CancellationToken>()))
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
        Assert.Contains("No backend available", result.Error);
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
        Assert.Contains("No backend available", result.Error);
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
        Assert.Contains("No backend available", result.Error);
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
        Assert.Contains("No backend available", result.Error);
    }

    #endregion

    #region get_type_outline 测试

    [Fact]
    public async Task GetTypeOutline_WithValidType_ReturnsOutline()
    {
        // Arrange
        var outline = new TypeOutlineResult
        {
            IsSuccess = true,
            TypeName = "MyNs.MyClass",
            Kind = "Class",
            Namespace = "MyNs",
            Accessibility = "Public",
            BaseType = "System.Object",
            Interfaces = new List<string> { "System.IDisposable" },
            Members = new List<MemberOutlineItem>
            {
                new() { Kind = "Method", Name = "DoWork", Signature = "Void DoWork()", Accessibility = "Public" },
                new() { Kind = "Property", Name = "Name", Signature = "String Name { get; set; }", Accessibility = "Public" }
            }
        };

        _mockBackend.Setup(b => b.GetTypeOutlineAsync("", "MyNs.MyClass", It.IsAny<CancellationToken>()))
            .ReturnsAsync(outline);

        // Act
        var result = await _tools.GetTypeOutline("MyNs.MyClass");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Class", result.Kind);
        Assert.Equal(2, result.Members!.Length);
        Assert.Single(result.Interfaces!);
    }

    [Fact]
    public async Task GetTypeOutline_TypeNotFound_ReturnsError()
    {
        _mockBackend.Setup(b => b.GetTypeOutlineAsync("", "Bad.Type", It.IsAny<CancellationToken>()))
            .ReturnsAsync(TypeOutlineResult.Failure("Type 'Bad.Type' not found"));

        var result = await _tools.GetTypeOutline("Bad.Type");

        Assert.False(result.Success);
        Assert.Contains("not found", result.Error!);
    }

    #endregion

    #region plan_chunking 测试

    [Fact]
    public async Task PlanChunking_WithValidType_ReturnsChunks()
    {
        // Arrange
        var chunks = new List<ChunkInfo>
        {
            new() { StartLine = 1, EndLine = 75, EstimatedChars = 5250 },
            new() { StartLine = 74, EndLine = 100, EstimatedChars = 1890 }
        };

        _mockBackend.Setup(b => b.PlanChunkingAsync("", "MyNs.MyClass", null, 6000, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChunkingPlanResult.Success(chunks, 100, 70));

        // Act
        var result = await _tools.PlanChunking("MyNs.MyClass");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Chunks!.Length);
        Assert.Equal(100, result.TotalLines);
        Assert.Equal(70, result.AvgCharsPerLine);
    }

    [Fact]
    public async Task PlanChunking_TypeNotFound_ReturnsError()
    {
        // Arrange
        _mockBackend.Setup(b => b.PlanChunkingAsync("", "NonExistent", null, 6000, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChunkingPlanResult.Failure("Type 'NonExistent' not found"));

        // Act
        var result = await _tools.PlanChunking("NonExistent");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Error!);
    }

    [Fact]
    public async Task PlanChunking_NoBackend_ReturnsError()
    {
        var registry = new Mock<IBackendRegistry>();
        registry.Setup(r => r.Get(It.IsAny<string?>())).Returns((IBackend?)null);
        var tools = new AnalysisTools(registry.Object);

        var result = await tools.PlanChunking("Any.Type");

        Assert.False(result.Success);
        Assert.Contains("No backend available", result.Error);
    }

    #endregion

    #region compare_assemblies 测试

    [Fact]
    public async Task CompareAssemblies_WithDifferences_ReturnsSummary()
    {
        // Arrange
        var leftMvid = "left-mvid";
        var rightMvid = "right-mvid";
        var summary = new CompareAssembliesSummary { Added = 1, Removed = 0, Modified = 1, Unchanged = 5 };
        var diffs = new List<CompareTypeDiffItem>
        {
            new() { TypeName = "Ns.NewClass", DiffType = "Added", MemberDiffs = new() },
            new() { TypeName = "Ns.ChangedClass", DiffType = "Modified", MemberDiffs = new()
            {
                new() { Name = "NewMethod", MemberType = "Method", DiffType = "Added" }
            }}
        };

        _mockBackend.Setup(b => b.CompareAssembliesAsync(leftMvid, rightMvid, null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompareAssembliesResult.Success(summary, diffs));

        // Act
        var result = await _tools.CompareAssemblies(leftMvid, rightMvid);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.Summary!.Added);
        Assert.Equal(1, result.Summary.Modified);
        Assert.Equal(2, result.TypeDiffs!.Length);
    }

    [Fact]
    public async Task CompareAssemblies_AssemblyNotFound_ReturnsError()
    {
        // Arrange
        _mockBackend.Setup(b => b.CompareAssembliesAsync(It.IsAny<string>(), It.IsAny<string>(), null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompareAssembliesResult.Failure("Assembly not found"));

        // Act
        var result = await _tools.CompareAssemblies("bad-mvid", "other-mvid");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Error!);
    }

    [Fact]
    public async Task CompareAssemblies_NoBackend_ReturnsError()
    {
        // Arrange
        var registry = new Mock<IBackendRegistry>();
        registry.Setup(r => r.Get(It.IsAny<string?>())).Returns((IBackend?)null);
        var tools = new AnalysisTools(registry.Object);

        // Act
        var result = await tools.CompareAssemblies("left", "right");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No backend available", result.Error);
    }

    #endregion

    #region batch_decompile 测试

    [Fact]
    public async Task BatchDecompile_WithMultipleMembers_ReturnsAll()
    {
        // Arrange
        var keys = new[] { "MyNamespace.ClassA", "MyNamespace.ClassB" };
        var items = new List<BatchDecompileItem>
        {
            new() { MemberKey = keys[0], Code = "public class ClassA { }", TotalLines = 1 },
            new() { MemberKey = keys[1], Code = "public class ClassB { }", TotalLines = 1 }
        };

        _mockBackend.Setup(b => b.BatchDecompileAsync("", keys, 200000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BatchDecompileResult.Success(items, false, 46, 2, 2));

        // Act
        var result = await _tools.BatchDecompile(keys);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Items!.Length);
        Assert.False(result.Truncated);
        Assert.Equal(2, result.Processed);
        Assert.Equal(2, result.Requested);
    }

    [Fact]
    public async Task BatchDecompile_WithCharBudgetExceeded_ReturnsTruncated()
    {
        // Arrange
        var keys = new[] { "MyNamespace.ClassA", "MyNamespace.ClassB" };
        var items = new List<BatchDecompileItem>
        {
            new() { MemberKey = keys[0], Code = "public class ClassA { }", TotalLines = 1 }
        };

        _mockBackend.Setup(b => b.BatchDecompileAsync("", keys, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BatchDecompileResult.Success(items, true, 23, 1, 2));

        // Act
        var result = await _tools.BatchDecompile(keys, maxTotalChars: 10);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Items!);
        Assert.True(result.Truncated);
        Assert.Equal(1, result.Processed);
        Assert.Equal(2, result.Requested);
    }

    [Fact]
    public async Task BatchDecompile_WithEmptyArray_ReturnsError()
    {
        // Act
        var result = await _tools.BatchDecompile(Array.Empty<string>());

        // Assert
        Assert.False(result.Success);
        Assert.Contains("empty", result.Error!);
    }

    [Fact]
    public async Task BatchDecompile_WithMethodKeys_ParsesCorrectly()
    {
        // Arrange
        var keys = new[] { "MyNamespace.MyClass::DoWork" };
        var items = new List<BatchDecompileItem>
        {
            new() { MemberKey = keys[0], Code = "public void DoWork() { }", TotalLines = 1 }
        };

        _mockBackend.Setup(b => b.BatchDecompileAsync("", keys, 200000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BatchDecompileResult.Success(items, false, 24, 1, 1));

        // Act
        var result = await _tools.BatchDecompile(keys);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Items!);
        Assert.Equal("MyNamespace.MyClass::DoWork", result.Items![0].MemberKey);
    }

    [Fact]
    public async Task BatchDecompile_WithInlineError_DoesNotFailBatch()
    {
        // Arrange
        var keys = new[] { "MyNamespace.ClassA", "NonExistent.Type" };
        var items = new List<BatchDecompileItem>
        {
            new() { MemberKey = keys[0], Code = "public class ClassA { }", TotalLines = 1 },
            new() { MemberKey = keys[1], Code = "// Error: Type not found", TotalLines = 1, IsError = true }
        };

        _mockBackend.Setup(b => b.BatchDecompileAsync("", keys, 200000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BatchDecompileResult.Success(items, false, 47, 2, 2));

        // Act
        var result = await _tools.BatchDecompile(keys);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Items!.Length);
        Assert.False(result.Items[0].IsError);
        Assert.True(result.Items[1].IsError);
    }

    [Fact]
    public async Task BatchDecompile_NoBackend_ReturnsError()
    {
        // Arrange
        var registry = new Mock<IBackendRegistry>();
        registry.Setup(r => r.Get(It.IsAny<string?>())).Returns((IBackend?)null);
        var tools = new AnalysisTools(registry.Object);

        // Act
        var result = await tools.BatchDecompile(new[] { "Any.Type" });

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No backend available", result.Error);
    }

    #endregion

    #region get_dependency_graph 测试

    [Fact]
    public async Task GetDependencyGraph_AssemblyLevel_ReturnsGraph()
    {
        // Arrange
        var mermaid = "graph TD\n  A --> B";
        _mockBackend.Setup(b => b.BuildDependencyGraphAsync("", "assembly", null, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DependencyGraphResult
            {
                IsSuccess = true,
                Level = "assembly",
                RootId = "root-id",
                TotalNodes = 5,
                InternalNodes = 1,
                ExternalNodes = 4,
                TotalEdges = 4,
                Mermaid = mermaid
            });

        // Act
        var result = await _tools.GetDependencyGraph(level: "assembly");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("assembly", result.Level);
        Assert.Equal(5, result.TotalNodes);
        Assert.Equal(4, result.ExternalNodes);
        Assert.Equal(mermaid, result.Mermaid);
    }

    [Fact]
    public async Task GetDependencyGraph_TypeLevel_ReturnsGraph()
    {
        // Arrange
        var rootType = "MyNamespace.MyClass";
        var mermaid = "graph TD\n  MyClass --> OtherClass";
        _mockBackend.Setup(b => b.BuildDependencyGraphAsync("", "type", rootType, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DependencyGraphResult
            {
                IsSuccess = true,
                Level = "type",
                RootId = rootType,
                TotalNodes = 3,
                InternalNodes = 2,
                ExternalNodes = 1,
                TotalEdges = 2,
                Mermaid = mermaid
            });

        // Act
        var result = await _tools.GetDependencyGraph(level: "type", rootType: rootType, maxDepth: 2);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("type", result.Level);
        Assert.Equal(rootType, result.RootId);
        Assert.Equal(mermaid, result.Mermaid);
    }

    [Fact]
    public async Task GetDependencyGraph_TypeLevelMissingRootType_ReturnsError()
    {
        // Act - level=type without rootType
        var result = await _tools.GetDependencyGraph(level: "type", rootType: null);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("rootType is required", result.Error);
    }

    #endregion
}
