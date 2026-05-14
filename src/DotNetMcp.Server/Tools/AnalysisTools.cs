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
    [McpServerTool(Name = "decompile_type"), Description("Decompile a type to readable source code. Use language='csharp' for high-level C# source, 'il' for low-level IL disassembly. Returns complete type definition including all members, properties, and nested types. Set preferOriginalSource=true to retrieve original source from PDB when available.")]
    public async Task<DecompileTypeResult> DecompileType(
        [Description("Full name of the type to decompile (e.g., 'MyNamespace.MyClass' or 'System.String')")] string typeName,
        [Description("MVID of the assembly. Omit to use the default loaded assembly.")] string? mvid = null,
        [Description("Output language: 'csharp' (readable source) or 'il' (low-level IL instructions)")] string language = "csharp",
        [Description("Prefer original source from PDB (embedded, local file, or SourceLink) over decompilation")] bool preferOriginalSource = false,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new DecompileTypeResult { Success = false, Error = err };

        var result = await backend.DecompileTypeAsync(mvid ?? "", typeName, language, preferOriginalSource);
        return new DecompileTypeResult
        {
            Success = result.IsSuccess,
            TypeName = result.Target,
            Code = result.Code,
            Error = result.IsSuccess ? null : $"Decompile failed for '{typeName}': {result.ErrorMessage}"
        };
    }

    /// <summary>
    /// 反编译方法
    /// </summary>
    [McpServerTool(Name = "decompile_method"), Description("Decompile a single method to source code. Faster than decompile_type for large classes when you only need one method. Returns method signature and body. Set preferOriginalSource=true to retrieve original source from PDB when available.")]
    public async Task<DecompileMethodResult> DecompileMethod(
        [Description("Full name of the type containing the method (e.g., 'MyNamespace.MyClass')")] string typeName,
        [Description("Name of the method to decompile (e.g., 'DoWork' or 'get_PropertyName')")] string methodName,
        [Description("MVID of the assembly. Omit to use the default loaded assembly.")] string? mvid = null,
        [Description("Output language: 'csharp' (readable source) or 'il' (low-level IL instructions)")] string language = "csharp",
        [Description("Prefer original source from PDB (embedded, local file, or SourceLink) over decompilation")] bool preferOriginalSource = false,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new DecompileMethodResult { Success = false, Error = err };

        var result = await backend.DecompileMethodAsync(mvid ?? "", typeName, methodName, language, preferOriginalSource);
        return new DecompileMethodResult
        {
            Success = result.IsSuccess,
            TypeName = result.Target,
            MethodName = methodName,
            Code = result.Code,
            Error = result.IsSuccess ? null : $"Decompile failed for '{typeName}.{methodName}': {result.ErrorMessage}"
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
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new FindReferencesResult { Success = false, Error = err, References = Array.Empty<ReferenceDto>() };

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
            Error = result.IsSuccess ? null : $"FindTypeReferences failed for '{typeName}': {result.ErrorMessage}"
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
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new FindReferencesResult { Success = false, Error = err, References = Array.Empty<ReferenceDto>() };

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
            Error = result.IsSuccess ? null : $"FindMethodCalls failed for '{typeName}.{methodName}': {result.ErrorMessage}"
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
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new CallGraphToolResult { Success = false, Error = err };

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
            Error = result.IsSuccess ? null : $"GetCallGraph failed for '{typeName}.{methodName}': {result.ErrorMessage}"
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
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new CFGToolResult { Success = false, Error = err };

        var result = await backend.BuildControlFlowGraphAsync(mvid ?? "", typeName, methodName, includeIL);
        return new CFGToolResult
        {
            Success = result.IsSuccess,
            MethodName = result.MethodName,
            BlockCount = result.BlockCount,
            EdgeCount = result.EdgeCount,
            Mermaid = result.Mermaid,
            Error = result.IsSuccess ? null : $"GetControlFlowGraph failed for '{typeName}.{methodName}': {result.ErrorMessage}"
        };
    }
    /// <summary>
    /// 获取类型大纲
    /// </summary>
    [McpServerTool(Name = "get_type_outline"), Description("Get a metadata-based structural outline of a type without full decompilation. Faster than decompile_type for quick orientation. Returns type kind, base type, interfaces, and all members with their signatures and accessibility.")]
    public async Task<TypeOutlineToolResult> GetTypeOutline(
        [Description("Full name of the type (e.g., 'MyNamespace.MyClass')")] string typeName,
        [Description("MVID of the assembly. Omit to use the default loaded assembly.")] string? mvid = null,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new TypeOutlineToolResult { Success = false, Error = err };

        var result = await backend.GetTypeOutlineAsync(mvid ?? "", typeName);
        return new TypeOutlineToolResult
        {
            Success = result.IsSuccess,
            TypeName = result.TypeName,
            Kind = result.Kind,
            Namespace = result.Namespace,
            Accessibility = result.Accessibility,
            BaseType = result.BaseType,
            Interfaces = result.Interfaces.ToArray(),
            Members = result.Members.Select(m => new MemberOutlineDto
            {
                Kind = m.Kind,
                Name = m.Name,
                Signature = m.Signature,
                Accessibility = m.Accessibility,
                IsStatic = m.IsStatic,
                IsVirtual = m.IsVirtual,
                IsAbstract = m.IsAbstract
            }).ToArray(),
            Error = result.IsSuccess ? null : $"GetTypeOutline failed for '{typeName}': {result.ErrorMessage}"
        };
    }

    /// <summary>
    /// 规划源码分块方案
    /// </summary>
    [McpServerTool(Name = "plan_chunking"), Description("Plan line-range chunks for a type or method's decompiled source. Useful for LLM-friendly paging of large source code. Returns chunk boundaries with estimated character counts based on a target character budget per chunk.")]
    public async Task<ChunkingPlanToolResult> PlanChunking(
        [Description("Full name of the type to plan chunks for")] string typeName,
        [Description("Optional method name. If specified, only the method's source is chunked.")] string? methodName = null,
        [Description("Target characters per chunk (default: 6000). Adjust based on LLM context budget.")] int targetChunkSize = 6000,
        [Description("Number of overlapping lines between consecutive chunks (default: 2)")] int overlap = 2,
        [Description("MVID of the assembly. Omit to use the default loaded assembly.")] string? mvid = null,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new ChunkingPlanToolResult { Success = false, Error = err };

        var result = await backend.PlanChunkingAsync(mvid ?? "", typeName, methodName, targetChunkSize, overlap);
        return new ChunkingPlanToolResult
        {
            Success = result.IsSuccess,
            Chunks = result.Chunks.Select(c => new ChunkInfoDto { StartLine = c.StartLine, EndLine = c.EndLine, EstimatedChars = c.EstimatedChars }).ToArray(),
            TotalLines = result.TotalLines,
            AvgCharsPerLine = result.AvgCharsPerLine,
            TotalEstimatedChars = result.TotalEstimatedChars,
            Error = result.IsSuccess ? null : $"PlanChunking failed for '{typeName}': {result.ErrorMessage}"
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
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new CompareAssembliesToolResult { Success = false, Error = err };

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
            Error = result.IsSuccess ? null : $"CompareAssemblies failed (left={leftMvid}, right={rightMvid}): {result.ErrorMessage}"
        };
    }

    /// <summary>
    /// 构建依赖图
    /// </summary>
    [McpServerTool(Name = "get_dependency_graph"), Description("Build a dependency graph for a loaded assembly at three granularities: 'assembly' (shows referenced assemblies and their versions), 'namespace' (shows inter-namespace coupling), or 'type' (shows inheritance and reference relationships from a root type up to maxDepth). Returns node/edge counts and a Mermaid diagram string for visualization. For level='type', rootType must be the full type name (e.g. 'MyNamespace.MyClass').")]
    public async Task<DependencyGraphToolResult> GetDependencyGraph(
        [Description("Granularity level: 'assembly' (default, inter-assembly refs), 'namespace' (inter-namespace coupling), or 'type' (type-level graph from rootType)")] string level = "assembly",
        [Description("Root type full name for level='type' (e.g. 'MyNamespace.MyClass'). Required when level is 'type'.")] string? rootType = null,
        [Description("Maximum traversal depth for level='type' (default: 3, max: 10). Ignored for assembly/namespace levels.")] int maxDepth = 3,
        [Description("MVID of the assembly. Omit to use the default loaded assembly.")] string? mvid = null,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new DependencyGraphToolResult { Success = false, Error = err };

        if (level == "type" && string.IsNullOrWhiteSpace(rootType))
            return new DependencyGraphToolResult { Success = false, Error = "rootType is required when level='type'. Provide the full type name (e.g. 'MyNamespace.MyClass')." };

        var result = await backend.BuildDependencyGraphAsync(mvid ?? "", level, rootType, maxDepth);
        return new DependencyGraphToolResult
        {
            Success = result.IsSuccess,
            Level = result.Level,
            RootId = result.RootId,
            TotalNodes = result.TotalNodes,
            ExternalNodes = result.ExternalNodes,
            TotalEdges = result.TotalEdges,
            Mermaid = result.Mermaid,
            Error = result.IsSuccess ? null : $"GetDependencyGraph failed (level={level}): {result.ErrorMessage}"
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
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new BatchDecompileToolResult { Success = false, Error = err };

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
            Error = result.IsSuccess ? null : $"BatchDecompile failed: {result.ErrorMessage}"
        };
    }

    /// <summary>
    /// 检测设计模式
    /// </summary>
    [McpServerTool(Name = "detect_design_patterns"), Description("Detect design patterns (Singleton, Factory, AbstractFactory, Observer, Builder, Strategy, Decorator) in a loaded assembly. If typeName is provided, only that type is analyzed; otherwise the entire assembly is scanned. Returns each detected pattern with its confidence level and supporting evidence.")]
    public async Task<DetectPatternsToolResult> DetectDesignPatterns(
        [Description("Optional full name of the type to analyze (e.g., 'MyNamespace.MyClass'). Omit to scan the entire assembly.")] string? typeName = null,
        [Description("MVID of the assembly. Omit to use the default loaded assembly.")] string? mvid = null,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new DetectPatternsToolResult { Success = false, Error = err };

        var result = await backend.DetectPatternsAsync(mvid ?? "", typeName);
        return new DetectPatternsToolResult
        {
            Success = result.IsSuccess,
            TotalCount = result.TotalCount,
            Summary = result.Summary,
            Patterns = result.Patterns?.Select(p => new PatternDto
            {
                PatternType = p.PatternType,
                TypeName = p.TypeName,
                Confidence = p.Confidence,
                Evidence = p.Evidence.ToArray(),
                RelatedTypes = p.RelatedTypes?.ToArray() ?? Array.Empty<string>()
            }).ToArray() ?? Array.Empty<PatternDto>(),
            Error = result.IsSuccess ? null : $"DetectDesignPatterns failed: {result.ErrorMessage}"
        };
    }

    /// <summary>
    /// 增强搜索 - 直接暴露 EnhancedSearchService 全部能力
    /// </summary>
    [McpServerTool(Name = "enhanced_search"), Description("Powerful assembly search with advanced syntax support. Supports: plain keywords, '+include -exclude' filtering, '=exact' exact match, '~fuzzy' fuzzy match, '/regex/' regular expressions, '0xToken' metadata token lookup, and automatic literal detection. Searches types, members, methods, fields, properties, events, and literals in one unified call.")]
    public async Task<EnhancedSearchToolResult> EnhancedSearch(
        [Description("Query string. Supports advanced syntax: '+include -exclude =exact ~fuzzy', '/regex/', '0xToken', or plain keywords.")] string query,
        [Description("MVID of the assembly. Omit to use the default loaded assembly.")] string? mvid = null,
        [Description("Search mode: 'auto' (default - autodetect), 'type', 'member', 'method', 'field', 'property', 'event', 'literal', 'token'.")] string mode = "auto",
        [Description("Filter by namespace prefix (e.g. 'System.Collections')")] string? namespaceFilter = null,
        [Description("Max results to return, default 100, max 1000")] int limit = 100,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new EnhancedSearchToolResult { Success = false, Error = err, Items = Array.Empty<SearchItemDto>() };

        limit = Math.Clamp(limit, 1, 1000);

        try
        {
            var result = await backend.EnhancedSearchAsync(mvid ?? "", query, mode, namespaceFilter, limit);
            return new EnhancedSearchToolResult
            {
                Success = true,
                Items = result.Items.Select(item => new SearchItemDto
                {
                    Id = item.Id,
                    Name = item.Name,
                    FullName = item.FullName,
                    Kind = item.Type,
                    DeclaringType = item.DeclaringType,
                    Namespace = item.Namespace,
                    ILOffset = item.ILOffset,
                    Value = item.Value,
                    Relevance = item.Relevance
                }).ToArray(),
                TotalCount = result.TotalCount,
                HasMore = result.HasMore,
                DurationMs = (long)result.SearchDuration.TotalMilliseconds,
                Mode = result.Mode.ToString()
            };
        }
        catch (Exception ex)
        {
            return new EnhancedSearchToolResult { Success = false, Error = $"EnhancedSearch failed: {ex.Message}", Items = Array.Empty<SearchItemDto>() };
        }
    }

    /// <summary>
    /// 查找类型的所有基类链（含接口）
    /// </summary>
    [McpServerTool(Name = "find_base_types"), Description("Find all base types (superclasses and interfaces) in the inheritance chain of a given type. Returns base classes from immediate parent up to System.Object, plus all implemented interfaces. External types (from referenced assemblies) are included with IsExternal=true. Useful for understanding a type's full contract and hierarchy.")]
    public async Task<InheritanceToolResult> FindBaseTypes(
        [Description("Full name of the type (e.g., 'MyNamespace.MyClass')")] string typeName,
        [Description("Include interfaces in the result (default: true)")] bool includeInterfaces = true,
        [Description("MVID of the assembly. Omit to use the default loaded assembly.")] string? mvid = null,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new InheritanceToolResult { Success = false, Error = err };

        var result = await backend.FindBaseTypesAsync(mvid ?? "", typeName, includeInterfaces);
        return new InheritanceToolResult
        {
            Success = result.IsSuccess,
            Types = result.Types.Select(t => new TypeRefDto
            {
                Id = t.Id, FullName = t.FullName, Namespace = t.Namespace, Kind = t.Kind, IsExternal = t.IsExternal
            }).ToArray(),
            TotalCount = result.TotalCount,
            Error = result.IsSuccess ? null : $"FindBaseTypes failed for '{typeName}': {result.ErrorMessage}"
        };
    }

    /// <summary>
    /// 查找继承自指定类型的所有派生类型
    /// </summary>
    [McpServerTool(Name = "find_derived_types"), Description("Find all types in the current module that inherit from (or implement) the given type. Use directOnly=true to return only immediate subclasses. Use directOnly=false (default) to recursively collect the entire subtype hierarchy. Useful for polymorphism analysis and finding all implementations.")]
    public async Task<InheritanceToolResult> FindDerivedTypes(
        [Description("Full name of the base type or interface (e.g., 'MyNamespace.BaseClass' or 'MyNamespace.IService')")] string typeName,
        [Description("Return only direct subclasses/implementors (default: false = return full hierarchy)")] bool directOnly = false,
        [Description("MVID of the assembly. Omit to use the default loaded assembly.")] string? mvid = null,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new InheritanceToolResult { Success = false, Error = err };

        var result = await backend.FindDerivedTypesAsync(mvid ?? "", typeName, directOnly);
        return new InheritanceToolResult
        {
            Success = result.IsSuccess,
            Types = result.Types.Select(t => new TypeRefDto
            {
                Id = t.Id, FullName = t.FullName, Namespace = t.Namespace, Kind = t.Kind, IsExternal = t.IsExternal
            }).ToArray(),
            TotalCount = result.TotalCount,
            Error = result.IsSuccess ? null : $"FindDerivedTypes failed for '{typeName}': {result.ErrorMessage}"
        };
    }

    /// <summary>
    /// 查找接口的所有实现
    /// </summary>
    [McpServerTool(Name = "get_implementations"), Description("Find all types in the current module that directly implement the specified interface. Returns only types that have the interface listed in their direct Interfaces collection (not through base class inheritance). Use find_derived_types for full hierarchy lookup.")]
    public async Task<InheritanceToolResult> GetImplementations(
        [Description("Full name of the interface type (e.g., 'MyNamespace.IService')")] string interfaceTypeName,
        [Description("MVID of the assembly. Omit to use the default loaded assembly.")] string? mvid = null,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new InheritanceToolResult { Success = false, Error = err };

        var result = await backend.GetImplementationsAsync(mvid ?? "", interfaceTypeName);
        return new InheritanceToolResult
        {
            Success = result.IsSuccess,
            Types = result.Types.Select(t => new TypeRefDto
            {
                Id = t.Id, FullName = t.FullName, Namespace = t.Namespace, Kind = t.Kind, IsExternal = t.IsExternal
            }).ToArray(),
            TotalCount = result.TotalCount,
            Error = result.IsSuccess ? null : $"GetImplementations failed for '{interfaceTypeName}': {result.ErrorMessage}"
        };
    }

    /// <summary>
    /// 查找方法的所有覆盖
    /// </summary>
    [McpServerTool(Name = "get_overrides"), Description("Find all override implementations of a virtual method across derived types in the current module. Only works on virtual/abstract methods. Returns each derived type that overrides the method with the same name and parameter signature. Useful for polymorphism analysis and finding all entry points through a virtual dispatch.")]
    public async Task<MethodInheritanceToolResult> GetOverrides(
        [Description("Full name of the declaring type (e.g., 'MyNamespace.BaseClass')")] string typeName,
        [Description("Name of the virtual or abstract method (e.g., 'Execute')")] string methodName,
        [Description("MVID of the assembly. Omit to use the default loaded assembly.")] string? mvid = null,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new MethodInheritanceToolResult { Success = false, Error = err };

        var result = await backend.GetOverridesAsync(mvid ?? "", typeName, methodName);
        return new MethodInheritanceToolResult
        {
            Success = result.IsSuccess,
            Methods = result.Methods.Select(m => new MethodRefDto
            {
                Id = m.Id, TypeFullName = m.TypeFullName, MethodName = m.MethodName, Signature = m.Signature
            }).ToArray(),
            TotalCount = result.TotalCount,
            Error = result.IsSuccess ? null : $"GetOverrides failed for '{typeName}.{methodName}': {result.ErrorMessage}"
        };
    }

    /// <summary>
    /// 查找方法的所有重载
    /// </summary>
    [McpServerTool(Name = "get_overloads"), Description("Find all overloads of a method within the same type (same method name, different parameter signatures). Returns each overload with its full signature. Useful when a method name is ambiguous and you need to identify the correct overload before calling decompile_method or get_overrides.")]
    public async Task<MethodInheritanceToolResult> GetOverloads(
        [Description("Full name of the type containing the method (e.g., 'MyNamespace.MyClass')")] string typeName,
        [Description("Name of the method to find overloads for (e.g., 'DoWork')")] string methodName,
        [Description("MVID of the assembly. Omit to use the default loaded assembly.")] string? mvid = null,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new MethodInheritanceToolResult { Success = false, Error = err };

        var result = await backend.GetOverloadsAsync(mvid ?? "", typeName, methodName);
        return new MethodInheritanceToolResult
        {
            Success = result.IsSuccess,
            Methods = result.Methods.Select(m => new MethodRefDto
            {
                Id = m.Id, TypeFullName = m.TypeFullName, MethodName = m.MethodName, Signature = m.Signature
            }).ToArray(),
            TotalCount = result.TotalCount,
            Error = result.IsSuccess ? null : $"GetOverloads failed for '{typeName}.{methodName}': {result.ErrorMessage}"
        };
    }

    /// <summary>
    /// 检测程序集混淆
    /// </summary>
    [McpServerTool(Name = "detect_obfuscation"), Description("Detect if assembly is obfuscated and identify the obfuscator. Returns score 0-100, confidence level, and top indicators (short names, invalid names, control flow flattening, proxy methods)")]
    public async Task<DetectObfuscationToolResult> DetectObfuscation(
        [Description("MVID of the assembly. Omit to use the default loaded assembly.")] string? mvid = null,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new DetectObfuscationToolResult { Success = false, Error = err };

        var result = await backend.DetectObfuscationAsync(mvid ?? "");
        if (!result.IsSuccess)
        {
            return new DetectObfuscationToolResult { Success = false, Error = $"DetectObfuscation failed: {result.ErrorMessage}" };
        }

        // TopIndicators: 按 Severity 排序取 Top 10
        var severityOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Critical"] = 0, ["High"] = 1, ["Medium"] = 2, ["Low"] = 3, ["Info"] = 4
        };
        var topIndicators = (result.Indicators ?? new List<ObfuscationIndicatorInfo>())
            .OrderBy(i => severityOrder.TryGetValue(i.Severity, out var order) ? order : 99)
            .Take(10)
            .Select(i => new IndicatorDto
            {
                Category = i.Category,
                Severity = i.Severity,
                Description = i.Description,
                Location = i.Location
            })
            .ToArray();

        var stats = result.Statistics;

        return new DetectObfuscationToolResult
        {
            Success = true,
            IsObfuscated = result.IsObfuscated,
            ObfuscationScore = result.ObfuscationScore,
            Confidence = result.Confidence,
            DetectedObfuscators = result.DetectedObfuscators?.ToArray() ?? Array.Empty<string>(),
            TopIndicators = topIndicators,
            Stats = stats == null ? null : new ObfStatsDto
            {
                TotalTypes = stats.TotalTypes,
                TotalMethods = stats.TotalMethods,
                TotalFields = stats.TotalFields,
                InvalidTypeNames = stats.InvalidTypeNames,
                InvalidMethodNames = stats.InvalidMethodNames,
                InvalidFieldNames = stats.InvalidFieldNames,
                ShortTypeNames = stats.ShortTypeNames,
                RandomTypeNames = stats.RandomTypeNames,
                FlattenedMethods = stats.FlattenedMethods,
                ProxyMethods = stats.ProxyMethods
            }
        };
    }

    /// <summary>
    /// 预热索引
    /// </summary>
    [McpServerTool(Name = "warm_index"), Description("Pre-build type and member indexes for faster subsequent queries. Useful for large assemblies before heavy analysis. Indexes are otherwise built on-demand at first access.")]
    public async Task<WarmIndexToolResult> WarmIndex(
        [Description("MVID or alias of the assembly. Omit to use default.")] string? mvid = null,
        [Description("Build type index (default true)")] bool typeIndex = true,
        [Description("Build member index (default true)")] bool memberIndex = true,
        [Description("Soft time budget in seconds. If exceeded, member index building is skipped.")] int? maxSeconds = null,
        [Description("Optional backend ID")] string? backendId = null)
    {
        var backend = _registry.TryGet(backendId, out var err);
        if (backend == null) return new WarmIndexToolResult { Success = false, Error = err };

        var result = await backend.WarmIndexAsync(mvid ?? "", typeIndex, memberIndex, maxSeconds);
        return new WarmIndexToolResult
        {
            Success = result.IsSuccess,
            TypeIndexBuilt = result.TypeIndexBuilt,
            MemberIndexBuilt = result.MemberIndexBuilt,
            TypeCount = result.TypeCount,
            MemberCount = result.MemberCount,
            ElapsedMs = result.ElapsedMs,
            MaxSecondsExceeded = result.MaxSecondsExceeded,
            Error = result.IsSuccess ? null : $"WarmIndex failed: {result.ErrorMessage}"
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

public record ChunkingPlanToolResult
{
    public bool Success { get; init; }
    public ChunkInfoDto[]? Chunks { get; init; }
    public int TotalLines { get; init; }
    public int AvgCharsPerLine { get; init; }
    public int TotalEstimatedChars { get; init; }
    public string? Error { get; init; }
}

public record ChunkInfoDto
{
    public int StartLine { get; init; }
    public int EndLine { get; init; }
    public int EstimatedChars { get; init; }
}

public record TypeOutlineToolResult
{
    public bool Success { get; init; }
    public string? TypeName { get; init; }
    public string? Kind { get; init; }
    public string? Namespace { get; init; }
    public string? Accessibility { get; init; }
    public string? BaseType { get; init; }
    public string[]? Interfaces { get; init; }
    public MemberOutlineDto[]? Members { get; init; }
    public string? Error { get; init; }
}

public record MemberOutlineDto
{
    public string? Kind { get; init; }
    public string? Name { get; init; }
    public string? Signature { get; init; }
    public string? Accessibility { get; init; }
    public bool IsStatic { get; init; }
    public bool IsVirtual { get; init; }
    public bool IsAbstract { get; init; }
}

public record DependencyGraphToolResult
{
    public bool Success { get; init; }
    public string? Level { get; init; }
    public string? RootId { get; init; }
    public int TotalNodes { get; init; }
    public int ExternalNodes { get; init; }
    public int TotalEdges { get; init; }
    public string? Mermaid { get; init; }
    public string? Error { get; init; }
}

public record DetectPatternsToolResult
{
    public bool Success { get; init; }
    public int TotalCount { get; init; }
    public Dictionary<string, int>? Summary { get; init; }
    public PatternDto[]? Patterns { get; init; }
    public string? Error { get; init; }
}

public record PatternDto
{
    public string? PatternType { get; init; }
    public string? TypeName { get; init; }
    public string? Confidence { get; init; }
    public string[]? Evidence { get; init; }
    public string[]? RelatedTypes { get; init; }
}

public record DetectObfuscationToolResult
{
    public bool Success { get; init; }
    public bool IsObfuscated { get; init; }
    public int ObfuscationScore { get; init; }
    public string? Confidence { get; init; }
    public string[]? DetectedObfuscators { get; init; }
    public IndicatorDto[]? TopIndicators { get; init; }
    public ObfStatsDto? Stats { get; init; }
    public string? Error { get; init; }
}

public record IndicatorDto
{
    public string? Category { get; init; }
    public string? Severity { get; init; }
    public string? Description { get; init; }
    public string? Location { get; init; }
}

public record ObfStatsDto
{
    public int TotalTypes { get; init; }
    public int TotalMethods { get; init; }
    public int TotalFields { get; init; }
    public int InvalidTypeNames { get; init; }
    public int InvalidMethodNames { get; init; }
    public int InvalidFieldNames { get; init; }
    public int ShortTypeNames { get; init; }
    public int RandomTypeNames { get; init; }
    public int FlattenedMethods { get; init; }
    public int ProxyMethods { get; init; }
}

public record EnhancedSearchToolResult
{
    public bool Success { get; init; }
    public required SearchItemDto[] Items { get; init; }
    public int TotalCount { get; init; }
    public bool HasMore { get; init; }
    public long DurationMs { get; init; }
    public string? Mode { get; init; }
    public string? Error { get; init; }
}

public record SearchItemDto
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? FullName { get; init; }
    public string? Kind { get; init; }
    public string? DeclaringType { get; init; }
    public string? Namespace { get; init; }
    public int? ILOffset { get; init; }
    public string? Value { get; init; }
    public double Relevance { get; init; }
}

public record InheritanceToolResult
{
    public bool Success { get; init; }
    public TypeRefDto[] Types { get; init; } = Array.Empty<TypeRefDto>();
    public int TotalCount { get; init; }
    public string? Error { get; init; }
}

public record TypeRefDto
{
    public string? Id { get; init; }
    public string? FullName { get; init; }
    public string? Namespace { get; init; }
    public string? Kind { get; init; }
    public bool IsExternal { get; init; }
}

public record MethodInheritanceToolResult
{
    public bool Success { get; init; }
    public MethodRefDto[] Methods { get; init; } = Array.Empty<MethodRefDto>();
    public int TotalCount { get; init; }
    public string? Error { get; init; }
}

public record MethodRefDto
{
    public string? Id { get; init; }
    public string? TypeFullName { get; init; }
    public string? MethodName { get; init; }
    public string? Signature { get; init; }
}

public record WarmIndexToolResult
{
    public bool Success { get; init; }
    public bool TypeIndexBuilt { get; init; }
    public bool MemberIndexBuilt { get; init; }
    public int TypeCount { get; init; }
    public int MemberCount { get; init; }
    public long ElapsedMs { get; init; }
    public bool MaxSecondsExceeded { get; init; }
    public string? Error { get; init; }
}
