using DotNetMcp.Backend.Services;
using DotNetMcp.Backend.Core.Analysis;
using DotNetMcp.Backend.Core.Context;
using RoslynPatchResult = DotNetMcp.Backend.Core.Modification.RoslynPatchResult;

namespace DotNetMcp.Server.Backend;

/// <summary>
/// 后端接口 - 统一本地和远程后端的操作
/// </summary>
public interface IBackend
{
    /// <summary>
    /// 后端唯一标识
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 后端名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 后端类型
    /// </summary>
    BackendType Type { get; }

    /// <summary>
    /// 后端是否可用
    /// </summary>
    bool IsHealthy { get; }

    /// <summary>
    /// 最后健康检查时间
    /// </summary>
    DateTime? LastHealthCheck { get; }

    /// <summary>
    /// 健康检查
    /// </summary>
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);

    // 程序集操作
    Task<AssemblyLoadResult> LoadAssemblyAsync(string path, IEnumerable<string>? searchPaths = null, CancellationToken cancellationToken = default);
    Task<bool> UnloadAssemblyAsync(string mvid, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssemblyListItem>> ListAssembliesAsync(CancellationToken cancellationToken = default);

    // 分析操作
    Task<DecompileResult> DecompileTypeAsync(string mvid, string typeName, string language = "csharp", bool preferOriginalSource = false, CancellationToken cancellationToken = default);
    Task<DecompileResult> DecompileMethodAsync(string mvid, string typeName, string methodName, string language = "csharp", bool preferOriginalSource = false, CancellationToken cancellationToken = default);
    Task<SearchTypesResult> SearchTypesAsync(string mvid, string keyword, string? namespaceFilter = null, int limit = 50, CancellationToken cancellationToken = default);
    Task<SearchStringsResult> SearchStringsAsync(string mvid, string query, string mode = "contains", int limit = 50, CancellationToken cancellationToken = default);
    Task<XRefResult> FindReferencesToTypeAsync(string mvid, string typeName, int limit = 50, CancellationToken cancellationToken = default);
    Task<XRefResult> FindCallsToMethodAsync(string mvid, string typeName, string methodName, int limit = 50, CancellationToken cancellationToken = default);
    Task<CallGraphResult> BuildCallGraphAsync(string mvid, string typeName, string methodName, string direction = "callees", int maxDepth = 3, int maxNodes = 100, CancellationToken cancellationToken = default);
    Task<CFGResult> BuildControlFlowGraphAsync(string mvid, string typeName, string methodName, bool includeIL = false, CancellationToken cancellationToken = default);

    // 批量操作
    Task<BatchDecompileResult> BatchDecompileAsync(string mvid, string[] memberKeys, int maxTotalChars = 200000, CancellationToken cancellationToken = default);

    // 对比操作
    Task<CompareAssembliesResult> CompareAssembliesAsync(string leftMvid, string rightMvid, string? namespaceFilter = null, bool includeUnchanged = false, CancellationToken cancellationToken = default);

    // 分块规划
    Task<ChunkingPlanResult> PlanChunkingAsync(string mvid, string typeName, string? methodName = null, int targetChunkSize = 6000, int overlap = 2, CancellationToken cancellationToken = default);

    // 类型大纲
    Task<TypeOutlineResult> GetTypeOutlineAsync(string mvid, string typeName, CancellationToken cancellationToken = default);

    // Patch 骨架生成
    Task<PatchSkeletonResult> GeneratePatchSkeletonAsync(string mvid, string typeName, string methodName, string[] patchKinds, CancellationToken cancellationToken = default);

    // 依赖图分析
    Task<DependencyGraphResult> BuildDependencyGraphAsync(string mvid, string level, string? rootType, int maxDepth, CancellationToken cancellationToken = default);

    // 设计模式检测
    Task<PatternDetectionServiceResult> DetectPatternsAsync(string mvid, string? typeName, CancellationToken cancellationToken = default);

    // 修改操作
    Task<ModificationResult> InjectAtEntryAsync(string mvid, string methodFullName, InjectionRequest request, CancellationToken cancellationToken = default);
    Task<ModificationResult> ReplaceMethodBodyAsync(string mvid, string methodFullName, InjectionRequest request, CancellationToken cancellationToken = default);
    Task<ModificationResult> AddTypeAsync(string mvid, TypeCreationRequest request, CancellationToken cancellationToken = default);
    Task<ModificationResult> SaveAssemblyAsync(string mvid, string outputPath, CancellationToken cancellationToken = default);

    // 混淆检测
    Task<ObfuscationDetectionServiceResult> DetectObfuscationAsync(string mvid, CancellationToken cancellationToken = default);

    #region Roslyn 修改
    /// <summary>
    /// 用 C# 源码替换方法体（Roslyn 编译 + Cecil 注入）
    /// </summary>
    Task<RoslynPatchResult> ReplaceMethodBodyWithCSharpAsync(
        string mvid,
        string methodFullName,
        string csharpBody,
        string[]? usings,
        bool allowUnsafe,
        CancellationToken cancellationToken = default);
    #endregion

    #region 增强搜索
    // 增强搜索 - 支持正则、高级语法（+/-/=/~）、Token 模式
    Task<EnhancedSearchResult> EnhancedSearchAsync(string mvid, string query, string mode, string? namespaceFilter, int limit, CancellationToken cancellationToken = default);
    #endregion

    #region 继承分析
    Task<InheritanceResult> FindBaseTypesAsync(string mvid, string typeName, bool includeInterfaces = true, CancellationToken cancellationToken = default);
    Task<InheritanceResult> FindDerivedTypesAsync(string mvid, string typeName, bool directOnly = false, CancellationToken cancellationToken = default);
    Task<InheritanceResult> GetImplementationsAsync(string mvid, string interfaceTypeName, CancellationToken cancellationToken = default);
    Task<MethodInheritanceResult> GetOverridesAsync(string mvid, string typeName, string methodName, CancellationToken cancellationToken = default);
    Task<MethodInheritanceResult> GetOverloadsAsync(string mvid, string typeName, string methodName, CancellationToken cancellationToken = default);
    #endregion

    #region Alias 管理（backend-local）
    Task<AliasOperationResult> RegisterAssemblyAliasAsync(string alias, string mvid, bool overwrite = false, CancellationToken cancellationToken = default);
    Task<AliasOperationResult> UnregisterAssemblyAliasAsync(string alias, CancellationToken cancellationToken = default);
    Task<ListAliasesResult> ListAssemblyAliasesAsync(CancellationToken cancellationToken = default);
    Task<RestorePersistedResult> RestorePersistedAssembliesAsync(CancellationToken cancellationToken = default);
    #endregion

    #region 索引管理
    Task<WarmIndexResult> WarmIndexAsync(string mvid, bool typeIndex = true, bool memberIndex = true, int? maxSeconds = null, CancellationToken cancellationToken = default);
    #endregion
}

/// <summary>
/// 后端类型
/// </summary>
public enum BackendType
{
    Local,
    Remote
}

/// <summary>
/// 程序集列表条目（ListAssembliesAsync 返回的 DTO）
/// </summary>
public record AssemblyListItem
{
    public required string Mvid { get; init; }
    public required string Name { get; init; }
    public required string Path { get; init; }
    public bool IsDefault { get; init; }
}

/// <summary>
/// 批量反编译结果
/// </summary>
public class BatchDecompileResult
{
    public bool IsSuccess { get; set; }
    public List<BatchDecompileItem> Items { get; set; } = new();
    public bool Truncated { get; set; }
    public int TotalCharsReturned { get; set; }
    public int Processed { get; set; }
    public int Requested { get; set; }
    public string? ErrorMessage { get; set; }

    public static BatchDecompileResult Success(List<BatchDecompileItem> items, bool truncated, int totalChars, int processed, int requested)
        => new() { IsSuccess = true, Items = items, Truncated = truncated, TotalCharsReturned = totalChars, Processed = processed, Requested = requested };

    public static BatchDecompileResult Failure(string error)
        => new() { IsSuccess = false, ErrorMessage = error };
}

/// <summary>
/// 批量反编译单项
/// </summary>
public class BatchDecompileItem
{
    public required string MemberKey { get; set; }
    public string? Code { get; set; }
    public string Language { get; set; } = "csharp";
    public int TotalLines { get; set; }
    public bool IsError { get; set; }
}

/// <summary>
/// 程序集对比结果
/// </summary>
public class CompareAssembliesResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public CompareAssembliesSummary Summary { get; set; } = new();
    public List<CompareTypeDiffItem> TypeDiffs { get; set; } = new();

    public static CompareAssembliesResult Success(CompareAssembliesSummary summary, List<CompareTypeDiffItem> diffs)
        => new() { IsSuccess = true, Summary = summary, TypeDiffs = diffs };

    public static CompareAssembliesResult Failure(string error)
        => new() { IsSuccess = false, ErrorMessage = error };
}

public class CompareAssembliesSummary
{
    public int Added { get; set; }
    public int Removed { get; set; }
    public int Modified { get; set; }
    public int Unchanged { get; set; }
}

public class CompareTypeDiffItem
{
    public required string TypeName { get; set; }
    public required string DiffType { get; set; }
    public List<CompareMemberDiffItem> MemberDiffs { get; set; } = new();
}

public class CompareMemberDiffItem
{
    public required string Name { get; set; }
    public required string MemberType { get; set; }
    public required string DiffType { get; set; }
}

/// <summary>
/// 分块规划结果
/// </summary>
public class ChunkingPlanResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ChunkInfo> Chunks { get; set; } = new();
    public int TotalLines { get; set; }
    public int AvgCharsPerLine { get; set; }
    public int TotalEstimatedChars { get; set; }

    public static ChunkingPlanResult Success(List<ChunkInfo> chunks, int totalLines, int avgCharsPerLine)
        => new() { IsSuccess = true, Chunks = chunks, TotalLines = totalLines, AvgCharsPerLine = avgCharsPerLine, TotalEstimatedChars = totalLines * avgCharsPerLine };

    public static ChunkingPlanResult Failure(string error)
        => new() { IsSuccess = false, ErrorMessage = error };
}

public class ChunkInfo
{
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public int EstimatedChars { get; set; }
}

/// <summary>
/// 类型大纲结果
/// </summary>
public class TypeOutlineResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TypeName { get; set; }
    public string? Kind { get; set; }
    public string? Namespace { get; set; }
    public string? Accessibility { get; set; }
    public string? BaseType { get; set; }
    public List<string> Interfaces { get; set; } = new();
    public List<MemberOutlineItem> Members { get; set; } = new();

    public static TypeOutlineResult Failure(string error)
        => new() { IsSuccess = false, ErrorMessage = error };
}

public class MemberOutlineItem
{
    public required string Kind { get; set; }
    public required string Name { get; set; }
    public string? Signature { get; set; }
    public string? Accessibility { get; set; }
    public bool IsStatic { get; set; }
    public bool IsVirtual { get; set; }
    public bool IsAbstract { get; set; }
}

/// <summary>
/// alias 操作结果
/// </summary>
public class AliasOperationResult
{
    public bool IsSuccess { get; set; }
    public string? Alias { get; set; }
    public string? Mvid { get; set; }
    public string? ErrorMessage { get; set; }

    public static AliasOperationResult Success(string alias, string? mvid = null)
        => new() { IsSuccess = true, Alias = alias, Mvid = mvid };

    public static AliasOperationResult Failure(string error)
        => new() { IsSuccess = false, ErrorMessage = error };
}

/// <summary>
/// 列出 alias 结果
/// </summary>
public class ListAliasesResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public List<AliasInfoDto> Aliases { get; set; } = new();

    public static ListAliasesResult Success(List<AliasInfoDto> aliases)
        => new() { IsSuccess = true, Aliases = aliases };

    public static ListAliasesResult Failure(string error)
        => new() { IsSuccess = false, ErrorMessage = error };
}

/// <summary>
/// 单条 alias 信息
/// </summary>
public class AliasInfoDto
{
    public required string Alias { get; set; }
    public required string Mvid { get; set; }
}

/// <summary>
/// 恢复持久化程序集结果
/// </summary>
public class RestorePersistedResult
{
    public bool IsSuccess { get; set; }
    public int RestoredCount { get; set; }
    public string? ErrorMessage { get; set; }

    public static RestorePersistedResult Success(int count)
        => new() { IsSuccess = true, RestoredCount = count };

    public static RestorePersistedResult Failure(string error)
        => new() { IsSuccess = false, ErrorMessage = error };
}
