using System.ComponentModel;
using ModelContextProtocol.Server;
using DotNetMcp.Server.Backend;
using DotNetMcp.Backend.Services;
using DotNetMcp.Backend.Core.Analysis;
using RoslynPatchResult = DotNetMcp.Backend.Core.Modification.RoslynPatchResult;

namespace DotNetMcp.Server.Tools;

/// <summary>
/// 程序集修改工具
/// </summary>
[McpServerToolType]
public sealed class ModificationTools
{
    private readonly IBackendRegistry _registry;

    public ModificationTools(IBackendRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// 在方法入口注入代码
    /// </summary>
    [McpServerTool(Name = "inject_at_entry"), Description("Inject IL instructions at method entry point. Use for adding logging, validation, or hooks before method execution. Example: inject Console.WriteLine to trace calls. Returns success status and error details if failed.")]
    public async Task<InjectionToolResult> InjectAtEntry(
        [Description("Full name of the method (e.g., 'MyNamespace.MyClass.MyMethod')")] string methodFullName,
        [Description("IL instructions to inject. Example: [{\"opCode\":\"ldstr\",\"stringValue\":\"Entering method\"},{\"opCode\":\"call\",\"stringValue\":\"System.Console::WriteLine\"}]")] InstructionDto[] instructions,
        [Description("Optional MVID of the assembly")] string? mvid = null,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new InjectionToolResult { Success = false, Error = err };

        var request = new InjectionRequest
        {
            Instructions = instructions.Select(i => new InstructionInfo
            {
                OpCode = i.OpCode,
                IntValue = i.IntValue,
                StringValue = i.StringValue
            }).ToList()
        };

        var result = await backend.InjectAtEntryAsync(mvid ?? "", methodFullName, request);
        return new InjectionToolResult
        {
            Success = result.IsSuccess,
            Message = result.IsSuccess ? "Injection successful" : null,
            Error = result.IsSuccess ? null : (result.ErrorCode != null
                ? $"[{result.ErrorCode}] {result.ErrorMessage}"
                : result.ErrorMessage)
        };
    }

    /// <summary>
    /// 替换方法体
    /// </summary>
    [McpServerTool(Name = "replace_method_body"), Description("Replace entire method body with new IL instructions. Must include 'ret' as final instruction. Use for patching, stubbing, or completely rewriting method logic. Replaces all existing IL.")]
    public async Task<InjectionToolResult> ReplaceMethodBody(
        [Description("Full name of the method (e.g., 'MyNamespace.MyClass.MyMethod')")] string methodFullName,
        [Description("New IL instructions replacing the body. Must end with 'ret'. Example: [{\"opCode\":\"ldc.i4\",\"intValue\":42},{\"opCode\":\"ret\"}] returns constant 42")] InstructionDto[] instructions,
        [Description("Optional MVID of the assembly")] string? mvid = null,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new InjectionToolResult { Success = false, Error = err };

        var request = new InjectionRequest
        {
            Instructions = instructions.Select(i => new InstructionInfo
            {
                OpCode = i.OpCode,
                IntValue = i.IntValue,
                StringValue = i.StringValue
            }).ToList()
        };

        var result = await backend.ReplaceMethodBodyAsync(mvid ?? "", methodFullName, request);
        return new InjectionToolResult
        {
            Success = result.IsSuccess,
            Message = result.IsSuccess ? "Method body replaced" : null,
            Error = result.IsSuccess ? null : (result.ErrorCode != null
                ? $"[{result.ErrorCode}] {result.ErrorMessage}"
                : result.ErrorMessage)
        };
    }

    /// <summary>
    /// 添加新类型
    /// </summary>
    [McpServerTool(Name = "add_type"), Description("Add a new type (class, interface, or struct) to the assembly. Creates an empty type definition that can be populated with members later. Returns full type name on success.")]
    public async Task<AddTypeToolResult> AddType(
        [Description("Namespace for the new type (e.g., 'MyApp.Models')")] string @namespace,
        [Description("Name of the new type (e.g., 'MyNewClass')")] string name,
        [Description("Type kind: 'class' (default), 'interface', or 'struct'")] string kind = "class",
        [Description("Optional MVID of the assembly")] string? mvid = null,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new AddTypeToolResult { Success = false, Error = err };

        var request = new TypeCreationRequest
        {
            Namespace = @namespace,
            Name = name,
            Kind = kind
        };

        var result = await backend.AddTypeAsync(mvid ?? "", request);
        return new AddTypeToolResult
        {
            Success = result.IsSuccess,
            FullName = result.IsSuccess ? $"{@namespace}.{name}" : null,
            Error = result.IsSuccess ? null : (result.ErrorCode != null
                ? $"[{result.ErrorCode}] {result.ErrorMessage}"
                : result.ErrorMessage)
        };
    }

    /// <summary>
    /// 保存程序集到文件
    /// </summary>
    [McpServerTool(Name = "save_assembly"), Description("Save the modified assembly to disk. Call after inject_at_entry, replace_method_body, or add_type operations to persist changes. Overwrites existing file at outputPath.")]
    public async Task<SaveAssemblyToolResult> SaveAssembly(
        [Description("Output file path (e.g., '/path/to/modified.dll'). Will overwrite if exists.")] string outputPath,
        [Description("Optional MVID of the assembly")] string? mvid = null,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new SaveAssemblyToolResult { Success = false, Error = err };

        var result = await backend.SaveAssemblyAsync(mvid ?? "", outputPath);
        return new SaveAssemblyToolResult
        {
            Success = result.IsSuccess,
            Path = result.IsSuccess ? outputPath : null,
            Error = result.IsSuccess ? null : (result.ErrorCode != null
                ? $"[{result.ErrorCode}] {result.ErrorMessage}"
                : result.ErrorMessage)
        };
    }

    /// <summary>
    /// 用 C# 源码替换方法体（Roslyn 编译 + Cecil 注入）
    /// </summary>
    [McpServerTool(Name = "replace_method_body_with_csharp"), Description("Replace a method body using C# source code instead of raw IL. Roslyn compiles the snippet, Cecil merges resulting IL into the target. Far easier than writing IL by hand. Returns compilation diagnostics if the C# fails to compile.")]
    public async Task<RoslynPatchToolResult> ReplaceMethodBodyWithCSharp(
        [Description("Full name of the method, e.g. 'MyNamespace.MyClass::MyMethod' or 'MyNamespace.MyClass.MyMethod'")] string methodFullName,
        [Description("C# method body (without the signature). Example: 'return x + 1;' or 'Console.WriteLine(\"hello\"); return 0;'")] string csharpBody,
        [Description("Optional MVID of the assembly. Omit to use the default loaded assembly.")] string? mvid = null,
        [Description("Optional extra using namespaces. Defaults to System, System.Collections.Generic, System.Linq, System.Text.")] string[]? usings = null,
        [Description("Allow unsafe C# code in the snippet (default false).")] bool allowUnsafe = false,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new RoslynPatchToolResult { Success = false, Error = err };

        var result = await backend.ReplaceMethodBodyWithCSharpAsync(mvid ?? "", methodFullName, csharpBody, usings, allowUnsafe);
        if (result.IsSuccess)
        {
            return new RoslynPatchToolResult
            {
                Success = true,
                Message = $"Method body replaced with {result.InstructionsReplaced} IL instructions",
                InstructionsReplaced = result.InstructionsReplaced
            };
        }

        return new RoslynPatchToolResult
        {
            Success = false,
            Error = result.ErrorMessage,
            Diagnostics = result.Diagnostics?.Select(d => $"[{d.Severity}] {d.Id} (line {d.Location}): {d.Message}").ToArray()
        };
    }

    /// <summary>
    /// 生成 Harmony Patch 骨架代码
    /// </summary>
    [McpServerTool(Name = "generate_patch_skeleton"), Description("Generate a Harmony patch skeleton for a method. Produces ready-to-use C# code with correct parameter types for Prefix, Postfix, Transpiler, or Finalizer patches. Useful for game modding (Unity, RimWorld, etc.).")]
    public async Task<PatchSkeletonToolResult> GeneratePatchSkeleton(
        [Description("Full name of the type containing the method")] string typeName,
        [Description("Name of the method to patch")] string methodName,
        [Description("Comma-separated patch kinds: 'Prefix', 'Postfix', 'Transpiler', 'Finalizer' (default: 'Prefix,Postfix')")] string patchKinds = "Prefix,Postfix",
        [Description("MVID of the assembly. Omit to use the default loaded assembly.")] string? mvid = null,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new PatchSkeletonToolResult { Success = false, Error = err };

        var kinds = patchKinds.Split(',').Select(k => k.Trim()).ToArray();
        var result = await backend.GeneratePatchSkeletonAsync(mvid ?? "", typeName, methodName, kinds);
        return new PatchSkeletonToolResult
        {
            Success = result.IsSuccess,
            Code = result.Code,
            Notes = result.Notes.ToArray(),
            Error = result.ErrorMessage
        };
    }
}

public record PatchSkeletonToolResult
{
    public bool Success { get; init; }
    public string? Code { get; init; }
    public string[]? Notes { get; init; }
    public string? Error { get; init; }
}

public record InstructionDto
{
    [Description("IL opcode name. Common opcodes: 'nop' (no-op), 'ldstr' (load string), 'ldc.i4' (load int), 'ldarg.0' (load this), 'ldnull' (load null), 'call' (call static), 'callvirt' (call virtual), 'ret' (return), 'pop' (discard top), 'dup' (duplicate top)")]
    public required string OpCode { get; init; }

    [Description("Integer operand for ldc.i4, ldarg, ldloc, stloc, etc. Example: ldc.i4 with IntValue=42 pushes 42 onto stack")]
    public int? IntValue { get; init; }

    [Description("String operand. For 'ldstr': the literal string to push. For 'call'/'callvirt': method reference like 'System.Console::WriteLine' or 'System.String::get_Length'")]
    public string? StringValue { get; init; }
}

public record InjectionToolResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? Error { get; init; }
}

public record AddTypeToolResult
{
    public bool Success { get; init; }
    public string? FullName { get; init; }
    public string? Error { get; init; }
}

public record SaveAssemblyToolResult
{
    public bool Success { get; init; }
    public string? Path { get; init; }
    public string? Error { get; init; }
}

public record RoslynPatchToolResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public int? InstructionsReplaced { get; init; }
    public string? Error { get; init; }
    public string[]? Diagnostics { get; init; }
}
