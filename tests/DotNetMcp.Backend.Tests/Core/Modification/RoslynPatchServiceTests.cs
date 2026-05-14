using DotNetMcp.Backend.Core.Compilation;
using DotNetMcp.Backend.Core.Context;
using DotNetMcp.Backend.Core.Modification;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Modification;

public class RoslynPatchServiceTests : IDisposable
{
    private readonly ReferenceAssemblyProvider _referenceProvider;
    private readonly CompilationService _compilationService;
    private readonly RoslynPatchService _service;

    // Track temp files to clean up
    private readonly List<string> _tempFiles = new();

    public RoslynPatchServiceTests()
    {
        _referenceProvider = new ReferenceAssemblyProvider();
        _compilationService = new CompilationService(_referenceProvider);
        _service = new RoslynPatchService(_compilationService);
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try { File.Delete(f); } catch { /* ignore */ }
        }
    }

    /// <summary>
    /// 用 Cecil 构建测试程序集，写到临时 dll，再异步加载成 AssemblyContext。
    /// </summary>
    private async Task<AssemblyContext> CreateTestContextAsync(
        string typeName,
        string methodName,
        bool returnsInt = true,
        IEnumerable<(string paramName, string paramType)>? parameters = null)
    {
        var asmDef = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("TestAssembly", new Version(1, 0)),
            "TestModule",
            ModuleKind.Dll);

        var module = asmDef.MainModule;
        var voidRef = module.ImportReference(typeof(void));
        var intRef = module.ImportReference(typeof(int));

        var typeDef = new TypeDefinition(
            "TestNs",
            typeName,
            TypeAttributes.Public | TypeAttributes.Class,
            module.ImportReference(typeof(object)));

        var returnType = returnsInt ? intRef : voidRef;
        var methodDef = new MethodDefinition(
            methodName,
            MethodAttributes.Public | MethodAttributes.Static,
            returnType);

        if (parameters != null)
        {
            foreach (var (pName, pTypeStr) in parameters)
            {
                var pType = pTypeStr == "int" ? intRef : module.ImportReference(typeof(string));
                methodDef.Parameters.Add(new ParameterDefinition(pName, ParameterAttributes.None, pType));
            }
        }

        var il = methodDef.Body.GetILProcessor();
        if (returnsInt)
        {
            il.Append(il.Create(OpCodes.Ldc_I4_0));
            il.Append(il.Create(OpCodes.Ret));
        }
        else
        {
            il.Append(il.Create(OpCodes.Ret));
        }

        typeDef.Methods.Add(methodDef);
        module.Types.Add(typeDef);

        var tempPath = Path.ChangeExtension(Path.GetTempFileName(), ".dll");
        _tempFiles.Add(tempPath);
        asmDef.Write(tempPath);
        asmDef.Dispose();

        var ctx = new AssemblyContext(tempPath);
        await ctx.LoadAsync();
        return ctx;
    }

    [Fact]
    public async Task ReplaceMethodBody_SimpleIntReturn_ShouldContainConstant42()
    {
        // Arrange
        using var ctx = await CreateTestContextAsync("FooClass", "Foo");

        // Act: replace with "return 42;"
        var result = _service.ReplaceMethodBody(ctx, "TestNs.FooClass", "Foo", "return 42;");

        // Assert
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.True(result.InstructionsReplaced > 0);

        // 验证 IL 里包含 ldc.i4 42
        var method = ctx.Assembly.MainModule.Types
            .First(t => t.Name == "FooClass")
            .Methods.First(m => m.Name == "Foo");

        var hasConst42 = method.Body.Instructions.Any(i =>
            (i.OpCode == OpCodes.Ldc_I4 && (int)i.Operand == 42) ||
            (i.OpCode == OpCodes.Ldc_I4_S && (sbyte)i.Operand == 42));

        Assert.True(hasConst42,
            $"Expected IL to contain ldc.i4 42, got: {string.Join(", ", method.Body.Instructions.Select(i => $"{i.OpCode} {i.Operand}"))}");
    }

    [Fact]
    public async Task ReplaceMethodBody_CompilationError_ShouldReturnDiagnostics()
    {
        // Arrange
        using var ctx = await CreateTestContextAsync("BarClass", "Bar");

        // Act: invalid C#
        var result = _service.ReplaceMethodBody(ctx, "TestNs.BarClass", "Bar", "this is bad C#");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Diagnostics);
        Assert.NotEmpty(result.Diagnostics!);
    }

    [Fact]
    public async Task ReplaceMethodBody_UsesSystemConsoleWriteLine_ImportReferenceSucceeds()
    {
        // Arrange: void method
        using var ctx = await CreateTestContextAsync("BazClass", "Baz", returnsInt: false);

        // Act: body uses Console.WriteLine — tests cross-module ImportReference
        var result = _service.ReplaceMethodBody(
            ctx,
            "TestNs.BazClass",
            "Baz",
            "System.Console.WriteLine(\"hello\");");

        // Assert
        Assert.True(result.IsSuccess, result.ErrorMessage);

        var method = ctx.Assembly.MainModule.Types
            .First(t => t.Name == "BazClass")
            .Methods.First(m => m.Name == "Baz");

        var hasCallInstruction = method.Body.Instructions.Any(i =>
            (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt) &&
            i.Operand is MethodReference mr &&
            mr.Name == "WriteLine");

        Assert.True(hasCallInstruction, "Expected IL to contain a call to WriteLine");
    }

    [Fact]
    public async Task ReplaceMethodBody_MethodNotFound_ShouldReturnFailure()
    {
        // Arrange
        using var ctx = await CreateTestContextAsync("QuxClass", "Qux");

        // Act: wrong method name
        var result = _service.ReplaceMethodBody(ctx, "TestNs.QuxClass", "NonExistent", "return 0;");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReplaceMethodBody_EmptyCsharpBody_ShouldReturnFailure()
    {
        // Arrange
        using var ctx = await CreateTestContextAsync("Cls", "M", returnsInt: false);

        // Act
        var result = _service.ReplaceMethodBody(ctx, "TestNs.Cls", "M", "   ");

        // Assert
        Assert.False(result.IsSuccess);
    }
}
