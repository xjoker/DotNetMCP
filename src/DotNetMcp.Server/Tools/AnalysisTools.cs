using System.ComponentModel;
using ModelContextProtocol.Server;
using DotNetMcp.Server.Backend;
using DotNetMcp.Backend.Services;

namespace DotNetMcp.Server.Tools;

/// <summary>
/// 分析工具
/// </summary>
[McpServerToolType]
public sealed class AnalysisTools
{
    private readonly IBackendRegistry _registry;

    public AnalysisTools(IBackendRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// 反编译类型为 C# 源码
    /// </summary>
    [McpServerTool(Name = "decompile_type"), Description("Decompile a type to readable source code. Use language='csharp' for high-level C# source, 'il' for low-level IL disassembly. Returns complete type definition including all members, properties, and nested types.")]
    public async Task<DecompileTypeResult> DecompileType(
        [Description("Full name of the type to decompile (e.g., 'MyNamespace.MyClass' or 'System.String')")] string typeName,
        [Description("MVID of the assembly. Omit to use the default loaded assembly.")] string? mvid = null,
        [Description("Output language: 'csharp' (readable source) or 'il' (low-level IL instructions)")] string language = "csharp",
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.Get(backendId);
        if (backend == null)
        {
            return new DecompileTypeResult { Success = false, Error = "No backend available" };
        }

        var result = await backend.DecompileTypeAsync(mvid ?? "", typeName, language);
        return new DecompileTypeResult
        {
            Success = result.IsSuccess,
            TypeName = result.Target,
            Code = result.Code,
            Error = result.ErrorMessage
        };
    }

    /// <summary>
    /// 反编译方法
    /// </summary>
    [McpServerTool(Name = "decompile_method"), Description("Decompile a single method to source code. Faster than decompile_type for large classes when you only need one method. Returns method signature and body.")]
    public async Task<DecompileMethodResult> DecompileMethod(
        [Description("Full name of the type containing the method (e.g., 'MyNamespace.MyClass')")] string typeName,
        [Description("Name of the method to decompile (e.g., 'DoWork' or 'get_PropertyName')")] string methodName,
        [Description("MVID of the assembly. Omit to use the default loaded assembly.")] string? mvid = null,
        [Description("Output language: 'csharp' (readable source) or 'il' (low-level IL instructions)")] string language = "csharp",
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.Get(backendId);
        if (backend == null)
        {
            return new DecompileMethodResult { Success = false, Error = "No backend available" };
        }

        var result = await backend.DecompileMethodAsync(mvid ?? "", typeName, methodName, language);
        return new DecompileMethodResult
        {
            Success = result.IsSuccess,
            TypeName = result.Target,
            MethodName = methodName,
            Code = result.Code,
            Error = result.ErrorMessage
        };
    }

    /// <summary>
    /// 查找类型引用
    /// </summary>
    [McpServerTool(Name = "find_type_references"), Description("Find all locations where a type is used. Detects field declarations, method parameters, local variables, return types, casts, and generic arguments. Useful for impact analysis before refactoring.")]
    public async Task<FindReferencesResult> FindTypeReferences(
        [Description("Full name of the type to find references for (e.g., 'MyNamespace.MyClass')")] string typeName,
        [Description("MVID of the assembly. Omit to use the default loaded assembly.")] string? mvid = null,
        [Description("Maximum number of results to return (default: 50)")] int limit = 50,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.Get(backendId);
        if (backend == null)
        {
            return new FindReferencesResult { Success = false, Error = "No backend available", References = Array.Empty<ReferenceDto>() };
        }

        var result = await backend.FindReferencesToTypeAsync(mvid ?? "", typeName, limit);
        return new FindReferencesResult
        {
            Success = result.IsSuccess,
            References = result.References?.Select(r => new ReferenceDto
            {
                SourceType = r.SourceTypeName,
                SourceMethod = r.SourceMemberName,
                TargetType = r.TargetName,
                TargetMember = r.TargetName,
                ReferenceKind = r.Kind.ToString()
            }).ToArray() ?? Array.Empty<ReferenceDto>(),
            TotalCount = result.TotalCount,
            Error = result.ErrorMessage
        };
    }

    /// <summary>
    /// 查找方法调用
    /// </summary>
    [McpServerTool(Name = "find_method_calls"), Description("Find all call sites for a method. Identifies every location that invokes the specified method. Useful for understanding usage patterns, impact analysis, and finding dead code.")]
    public async Task<FindReferencesResult> FindMethodCalls(
        [Description("Full name of the type containing the method (e.g., 'MyNamespace.MyClass')")] string typeName,
        [Description("Name of the method (e.g., 'DoWork')")] string methodName,
        [Description("MVID of the assembly. Omit to use the default loaded assembly.")] string? mvid = null,
        [Description("Maximum number of results to return (default: 50)")] int limit = 50,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.Get(backendId);
        if (backend == null)
        {
            return new FindReferencesResult { Success = false, Error = "No backend available", References = Array.Empty<ReferenceDto>() };
        }

        var result = await backend.FindCallsToMethodAsync(mvid ?? "", typeName, methodName, limit);
        return new FindReferencesResult
        {
            Success = result.IsSuccess,
            References = result.References?.Select(r => new ReferenceDto
            {
                SourceType = r.SourceTypeName,
                SourceMethod = r.SourceMemberName,
                TargetType = r.TargetName,
                TargetMember = r.TargetName,
                ReferenceKind = r.Kind.ToString()
            }).ToArray() ?? Array.Empty<ReferenceDto>(),
            TotalCount = result.TotalCount,
            Error = result.ErrorMessage
        };
    }

    /// <summary>
    /// 构建调用图
    /// </summary>
    [McpServerTool(Name = "get_call_graph"), Description("Build a call hierarchy tree from a method. Use direction='callees' to see what this method calls (outgoing); use direction='callers' to see what calls this method (incoming). Returns methods organized by depth level.")]
    public async Task<CallGraphToolResult> GetCallGraph(
        [Description("Full name of the type containing the method (e.g., 'MyNamespace.MyClass')")] string typeName,
        [Description("Name of the method (e.g., 'DoWork')")] string methodName,
        [Description("MVID of the assembly. Omit to use the default loaded assembly.")] string? mvid = null,
        [Description("Direction: 'callees' (what this method calls) or 'callers' (what calls this method)")] string direction = "callees",
        [Description("Maximum depth to traverse (default: 3). Higher values may be slow for complex code.")] int maxDepth = 3,
        [Description("Maximum number of nodes to return (default: 100)")] int maxNodes = 100,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.Get(backendId);
        if (backend == null)
        {
            return new CallGraphToolResult { Success = false, Error = "No backend available" };
        }

        var result = await backend.BuildCallGraphAsync(mvid ?? "", typeName, methodName, direction, maxDepth, maxNodes);
        return new CallGraphToolResult
        {
            Success = result.IsSuccess,
            StartMethod = result.StartMethod,
            Levels = result.Levels?.Select(l => new CallGraphLevelDto
            {
                Depth = l.Depth,
                Methods = l.Methods.ToArray()
            }).ToArray(),
            MaxDepthReached = result.MaxDepthReached,
            Error = result.ErrorMessage
        };
    }

    /// <summary>
    /// 构建控制流图
    /// </summary>
    [McpServerTool(Name = "get_control_flow_graph"), Description("Build a control flow graph (CFG) showing execution paths through a method. Returns block count, edge count, and a Mermaid diagram string for visualization. Use includeIL=true to see IL instructions within each basic block.")]
    public async Task<CFGToolResult> GetControlFlowGraph(
        [Description("Full name of the type containing the method (e.g., 'MyNamespace.MyClass')")] string typeName,
        [Description("Name of the method (e.g., 'DoWork')")] string methodName,
        [Description("MVID of the assembly. Omit to use the default loaded assembly.")] string? mvid = null,
        [Description("Include IL instructions in each basic block (default: false). Set true for detailed analysis.")] bool includeIL = false,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.Get(backendId);
        if (backend == null)
        {
            return new CFGToolResult { Success = false, Error = "No backend available" };
        }

        var result = await backend.BuildControlFlowGraphAsync(mvid ?? "", typeName, methodName, includeIL);
        return new CFGToolResult
        {
            Success = result.IsSuccess,
            MethodName = result.MethodName,
            BlockCount = result.BlockCount,
            EdgeCount = result.EdgeCount,
            Mermaid = result.Mermaid,
            Error = result.ErrorMessage
        };
    }
    /// <summary>
    /// 对比两个已加载的程序集
    /// </summary>
    [McpServerTool(Name = "compare_assemblies"), Description("Compare two loaded assemblies to find structural differences. Detects added, removed, and modified types and their members. Load two versions of the same assembly first, then compare by their MVIDs.")]
    public async Task<CompareAssembliesToolResult> CompareAssemblies(
        [Description("MVID of the left (original/old) assembly")] string leftMvid,
        [Description("MVID of the right (modified/new) assembly")] string rightMvid,
        [Description("Filter by namespace prefix (e.g., 'MyApp.Services'). Only types in this namespace are compared.")] string? namespaceFilter = null,
        [Description("Include unchanged types in the result (default: false)")] bool includeUnchanged = false,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.Get(backendId);
        if (backend == null)
        {
            return new CompareAssembliesToolResult { Success = false, Error = "No backend available" };
        }

        var result = await backend.CompareAssembliesAsync(leftMvid, rightMvid, namespaceFilter, includeUnchanged);
        return new CompareAssembliesToolResult
        {
            Success = result.IsSuccess,
            Summary = result.Summary,
            TypeDiffs = result.TypeDiffs.Select(t => new CompareTypeDiffDto
            {
                TypeName = t.TypeName,
                DiffType = t.DiffType,
                MemberDiffs = t.MemberDiffs.Select(m => new CompareMemberDiffDto
                {
                    Name = m.Name,
                    MemberType = m.MemberType,
                    DiffType = m.DiffType
                }).ToArray()
            }).ToArray(),
            Error = result.ErrorMessage
        };
    }

    /// <summary>
    /// 批量反编译多个成员
    /// </summary>
    [McpServerTool(Name = "batch_decompile"), Description("Decompile multiple types or methods in a single call with a character budget. Use 'TypeName' for types or 'TypeName::MethodName' for methods. Stops adding results when maxTotalChars is exceeded. Individual errors are returned inline without failing the batch.")]
    public async Task<BatchDecompileToolResult> BatchDecompile(
        [Description("Array of member keys. Use 'Namespace.TypeName' for types, 'Namespace.TypeName::MethodName' for methods.")] string[] memberKeys,
        [Description("MVID of the assembly. Omit to use the default loaded assembly.")] string? mvid = null,
        [Description("Maximum total characters to return across all results (default: 200000). Once exceeded, remaining members are skipped.")] int maxTotalChars = 200000,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.Get(backendId);
        if (backend == null)
        {
            return new BatchDecompileToolResult { Success = false, Error = "No backend available" };
        }

        if (memberKeys.Length == 0)
        {
            return new BatchDecompileToolResult { Success = false, Error = "memberKeys array cannot be empty" };
        }

        var result = await backend.BatchDecompileAsync(mvid ?? "", memberKeys, maxTotalChars);
        return new BatchDecompileToolResult
        {
            Success = result.IsSuccess,
            Items = result.Items.Select(i => new BatchDecompileItemDto
            {
                MemberKey = i.MemberKey,
                Code = i.Code,
                TotalLines = i.TotalLines,
                IsError = i.IsError
            }).ToArray(),
            Truncated = result.Truncated,
            TotalCharsReturned = result.TotalCharsReturned,
            Processed = result.Processed,
            Requested = result.Requested,
            Error = result.ErrorMessage
        };
    }
}

public record DecompileTypeResult
{
    public bool Success { get; init; }
    public string? TypeName { get; init; }
    public string? Code { get; init; }
    public string? Error { get; init; }
}

public record DecompileMethodResult
{
    public bool Success { get; init; }
    public string? TypeName { get; init; }
    public string? MethodName { get; init; }
    public string? Code { get; init; }
    public string? Error { get; init; }
}

public record FindReferencesResult
{
    public bool Success { get; init; }
    public required ReferenceDto[] References { get; init; }
    public int TotalCount { get; init; }
    public string? Error { get; init; }
}

public record ReferenceDto
{
    public string? SourceType { get; init; }
    public string? SourceMethod { get; init; }
    public string? TargetType { get; init; }
    public string? TargetMember { get; init; }
    public string? ReferenceKind { get; init; }
}

public record CallGraphToolResult
{
    public bool Success { get; init; }
    public string? StartMethod { get; init; }
    public CallGraphLevelDto[]? Levels { get; init; }
    public bool MaxDepthReached { get; init; }
    public string? Error { get; init; }
}

public record CallGraphLevelDto
{
    public int Depth { get; init; }
    public required string[] Methods { get; init; }
}

public record CFGToolResult
{
    public bool Success { get; init; }
    public string? MethodName { get; init; }
    public int BlockCount { get; init; }
    public int EdgeCount { get; init; }
    public string? Mermaid { get; init; }
    public string? Error { get; init; }
}

public record BatchDecompileToolResult
{
    public bool Success { get; init; }
    public BatchDecompileItemDto[]? Items { get; init; }
    public bool Truncated { get; init; }
    public int TotalCharsReturned { get; init; }
    public int Processed { get; init; }
    public int Requested { get; init; }
    public string? Error { get; init; }
}

public record BatchDecompileItemDto
{
    public string? MemberKey { get; init; }
    public string? Code { get; init; }
    public int TotalLines { get; init; }
    public bool IsError { get; init; }
}

public record CompareAssembliesToolResult
{
    public bool Success { get; init; }
    public CompareAssembliesSummary? Summary { get; init; }
    public CompareTypeDiffDto[]? TypeDiffs { get; init; }
    public string? Error { get; init; }
}

public record CompareTypeDiffDto
{
    public string? TypeName { get; init; }
    public string? DiffType { get; init; }
    public CompareMemberDiffDto[]? MemberDiffs { get; init; }
}

public record CompareMemberDiffDto
{
    public string? Name { get; init; }
    public string? MemberType { get; init; }
    public string? DiffType { get; init; }
}
