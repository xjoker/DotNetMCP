using DotNetMcp.Server.Backend;
using DotNetMcp.Server.Tools;
using DotNetMcp.Backend.Services;
using DotNetMcp.Backend.Core.Analysis;

namespace DotNetMcp.Server.Tests.Tools;

/// <summary>
/// ModificationTools 单元测试
/// </summary>
public class ModificationToolsTests
{
    private readonly Mock<IBackendRegistry> _mockRegistry;
    private readonly Mock<IBackend> _mockBackend;
    private readonly ModificationTools _tools;

    public ModificationToolsTests()
    {
        _mockRegistry = new Mock<IBackendRegistry>();
        _mockBackend = new Mock<IBackend>();
        _mockBackend.Setup(b => b.Id).Returns("local");
        _mockRegistry.Setup(r => r.Get(It.IsAny<string?>())).Returns(_mockBackend.Object);
        _tools = new ModificationTools(_mockRegistry.Object);
    }

    #region inject_at_entry 测试

    [Fact]
    public async Task InjectAtEntry_WithValidInstructions_ReturnsSuccess()
    {
        // Arrange
        var methodFullName = "MyNamespace.MyClass.MyMethod";
        var instructions = new[]
        {
            new InstructionDto { OpCode = "nop" },
            new InstructionDto { OpCode = "ldstr", StringValue = "Hello" }
        };

        _mockBackend.Setup(b => b.InjectAtEntryAsync("", methodFullName, It.IsAny<InjectionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModificationResult { IsSuccess = true });

        // Act
        var result = await _tools.InjectAtEntry(methodFullName, instructions);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Injection successful", result.Message);
    }

    [Fact]
    public async Task InjectAtEntry_WithInvalidOpCode_ReturnsError()
    {
        // Arrange
        var methodFullName = "MyNamespace.MyClass.MyMethod";
        var instructions = new[] { new InstructionDto { OpCode = "invalid_opcode" } };

        _mockBackend.Setup(b => b.InjectAtEntryAsync("", methodFullName, It.IsAny<InjectionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModificationResult { IsSuccess = false, ErrorMessage = "Invalid opcode: invalid_opcode" });

        // Act
        var result = await _tools.InjectAtEntry(methodFullName, instructions);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Invalid opcode", result.Error);
    }

    [Fact]
    public async Task InjectAtEntry_WithNoBackend_ReturnsError()
    {
        // Arrange
        _mockRegistry.Setup(r => r.Get(It.IsAny<string?>())).Returns((IBackend?)null);
        var tools = new ModificationTools(_mockRegistry.Object);
        var instructions = new[] { new InstructionDto { OpCode = "nop" } };

        // Act
        var result = await tools.InjectAtEntry("Any.Method", instructions);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No backend available", result.Error);
    }

    [Fact]
    public async Task InjectAtEntry_WithIntValue_PassesCorrectly()
    {
        // Arrange
        var methodFullName = "MyClass.Method";
        var instructions = new[] { new InstructionDto { OpCode = "ldc.i4", IntValue = 42 } };

        _mockBackend.Setup(b => b.InjectAtEntryAsync("", methodFullName, It.Is<InjectionRequest>(r =>
            r.Instructions.Count == 1 && r.Instructions[0].IntValue == 42), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModificationResult { IsSuccess = true });

        // Act
        var result = await _tools.InjectAtEntry(methodFullName, instructions);

        // Assert
        Assert.True(result.Success);
    }

    #endregion

    #region replace_method_body 测试

    [Fact]
    public async Task ReplaceMethodBody_WithValidInstructions_ReturnsSuccess()
    {
        // Arrange
        var methodFullName = "MyNamespace.MyClass.MyMethod";
        var instructions = new[]
        {
            new InstructionDto { OpCode = "ldc.i4", IntValue = 1 },
            new InstructionDto { OpCode = "ret" }
        };

        _mockBackend.Setup(b => b.ReplaceMethodBodyAsync("", methodFullName, It.IsAny<InjectionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModificationResult { IsSuccess = true });

        // Act
        var result = await _tools.ReplaceMethodBody(methodFullName, instructions);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Method body replaced", result.Message);
    }

    [Fact]
    public async Task ReplaceMethodBody_WithNonExistentMethod_ReturnsError()
    {
        // Arrange
        var methodFullName = "NonExistent.Method";
        var instructions = new[] { new InstructionDto { OpCode = "ret" } };

        _mockBackend.Setup(b => b.ReplaceMethodBodyAsync("", methodFullName, It.IsAny<InjectionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModificationResult { IsSuccess = false, ErrorMessage = "Method not found" });

        // Act
        var result = await _tools.ReplaceMethodBody(methodFullName, instructions);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Method not found", result.Error);
    }

    [Fact]
    public async Task ReplaceMethodBody_WithNoBackend_ReturnsError()
    {
        // Arrange
        _mockRegistry.Setup(r => r.Get(It.IsAny<string?>())).Returns((IBackend?)null);
        var tools = new ModificationTools(_mockRegistry.Object);
        var instructions = new[] { new InstructionDto { OpCode = "ret" } };

        // Act
        var result = await tools.ReplaceMethodBody("Any.Method", instructions);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No backend available", result.Error);
    }

    #endregion

    #region add_type 测试

    [Fact]
    public async Task AddType_WithClass_ReturnsSuccess()
    {
        // Arrange
        var ns = "MyNamespace";
        var name = "NewClass";

        _mockBackend.Setup(b => b.AddTypeAsync("", It.Is<TypeCreationRequest>(r =>
            r.Namespace == ns && r.Name == name && r.Kind == "class"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModificationResult { IsSuccess = true });

        // Act
        var result = await _tools.AddType(ns, name);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("MyNamespace.NewClass", result.FullName);
    }

    [Fact]
    public async Task AddType_WithInterface_CreatesInterface()
    {
        // Arrange
        var ns = "MyNamespace";
        var name = "INewInterface";

        _mockBackend.Setup(b => b.AddTypeAsync("", It.Is<TypeCreationRequest>(r => r.Kind == "interface"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModificationResult { IsSuccess = true });

        // Act
        var result = await _tools.AddType(ns, name, kind: "interface");

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public async Task AddType_WithStruct_CreatesStruct()
    {
        // Arrange
        var ns = "MyNamespace";
        var name = "NewStruct";

        _mockBackend.Setup(b => b.AddTypeAsync("", It.Is<TypeCreationRequest>(r => r.Kind == "struct"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModificationResult { IsSuccess = true });

        // Act
        var result = await _tools.AddType(ns, name, kind: "struct");

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public async Task AddType_WithNoBackend_ReturnsError()
    {
        // Arrange
        _mockRegistry.Setup(r => r.Get(It.IsAny<string?>())).Returns((IBackend?)null);
        var tools = new ModificationTools(_mockRegistry.Object);

        // Act
        var result = await tools.AddType("NS", "Type");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No backend available", result.Error);
    }

    #endregion

    #region save_assembly 测试

    [Fact]
    public async Task SaveAssembly_WithValidPath_ReturnsSuccess()
    {
        // Arrange
        var outputPath = "/output/modified.dll";

        _mockBackend.Setup(b => b.SaveAssemblyAsync("", outputPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModificationResult { IsSuccess = true });

        // Act
        var result = await _tools.SaveAssembly(outputPath);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(outputPath, result.Path);
    }

    [Fact]
    public async Task SaveAssembly_WithInvalidPath_ReturnsError()
    {
        // Arrange
        var outputPath = "/invalid/path/file.dll";

        _mockBackend.Setup(b => b.SaveAssemblyAsync("", outputPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModificationResult { IsSuccess = false, ErrorMessage = "Directory not found" });

        // Act
        var result = await _tools.SaveAssembly(outputPath);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Directory not found", result.Error);
    }

    [Fact]
    public async Task SaveAssembly_WithSpecificMvid_UsesCorrectAssembly()
    {
        // Arrange
        var mvid = Guid.NewGuid().ToString();
        var outputPath = "/output/file.dll";

        _mockBackend.Setup(b => b.SaveAssemblyAsync(mvid, outputPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModificationResult { IsSuccess = true });

        // Act
        await _tools.SaveAssembly(outputPath, mvid: mvid);

        // Assert
        _mockBackend.Verify(b => b.SaveAssemblyAsync(mvid, outputPath, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveAssembly_WithNoBackend_ReturnsError()
    {
        // Arrange
        _mockRegistry.Setup(r => r.Get(It.IsAny<string?>())).Returns((IBackend?)null);
        var tools = new ModificationTools(_mockRegistry.Object);

        // Act
        var result = await tools.SaveAssembly("/any/path.dll");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No backend available", result.Error);
    }

    #endregion

    #region generate_patch_skeleton 测试

    [Fact]
    public async Task GeneratePatchSkeleton_WithValidMethod_ReturnsCode()
    {
        // Arrange
        var patchResult = PatchSkeletonResult.Success(
            "using HarmonyLib;\n[HarmonyPatch]\npublic class MyPatch { }",
            new List<string> { "Add HarmonyLib NuGet package" });

        _mockBackend.Setup(b => b.GeneratePatchSkeletonAsync("", "MyNs.MyClass", "DoWork",
            It.Is<string[]>(k => k.Contains("Prefix")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(patchResult);

        // Act
        var result = await _tools.GeneratePatchSkeleton("MyNs.MyClass", "DoWork", "Prefix");

        // Assert
        Assert.True(result.Success);
        Assert.Contains("HarmonyPatch", result.Code!);
        Assert.NotEmpty(result.Notes!);
    }

    [Fact]
    public async Task GeneratePatchSkeleton_MethodNotFound_ReturnsError()
    {
        _mockBackend.Setup(b => b.GeneratePatchSkeletonAsync("", "MyNs.MyClass", "Bad",
            It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PatchSkeletonResult.Failure("Method 'Bad' not found"));

        var result = await _tools.GeneratePatchSkeleton("MyNs.MyClass", "Bad");

        Assert.False(result.Success);
        Assert.Contains("not found", result.Error!);
    }

    [Fact]
    public async Task GeneratePatchSkeleton_NoBackend_ReturnsError()
    {
        var registry = new Mock<IBackendRegistry>();
        registry.Setup(r => r.Get(It.IsAny<string?>())).Returns((IBackend?)null);
        var tools = new ModificationTools(registry.Object);

        var result = await tools.GeneratePatchSkeleton("Any.Type", "AnyMethod");

        Assert.False(result.Success);
        Assert.Contains("No backend available", result.Error);
    }

    #endregion
}
