using DotNetMcp.Backend.Services;
using DotNetMcp.Backend.Core.Analysis;
using DotNetMcp.Backend.Core.Context;

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
    Task<IReadOnlyList<AssemblyInfo>> ListAssembliesAsync(CancellationToken cancellationToken = default);

    // 分析操作
    Task<DecompileResult> DecompileTypeAsync(string mvid, string typeName, string language = "csharp", CancellationToken cancellationToken = default);
    Task<DecompileResult> DecompileMethodAsync(string mvid, string typeName, string methodName, string language = "csharp", CancellationToken cancellationToken = default);
    Task<SearchTypesResult> SearchTypesAsync(string mvid, string keyword, string? namespaceFilter = null, int limit = 50, CancellationToken cancellationToken = default);
    Task<SearchStringsResult> SearchStringsAsync(string mvid, string query, string mode = "contains", int limit = 50, CancellationToken cancellationToken = default);
    Task<XRefResult> FindReferencesToTypeAsync(string mvid, string typeName, int limit = 50, CancellationToken cancellationToken = default);
    Task<XRefResult> FindCallsToMethodAsync(string mvid, string typeName, string methodName, int limit = 50, CancellationToken cancellationToken = default);
    Task<CallGraphResult> BuildCallGraphAsync(string mvid, string typeName, string methodName, string direction = "callees", int maxDepth = 3, int maxNodes = 100, CancellationToken cancellationToken = default);
    Task<CFGResult> BuildControlFlowGraphAsync(string mvid, string typeName, string methodName, bool includeIL = false, CancellationToken cancellationToken = default);

    // 修改操作
    Task<ModificationResult> InjectAtEntryAsync(string mvid, string methodFullName, InjectionRequest request, CancellationToken cancellationToken = default);
    Task<ModificationResult> ReplaceMethodBodyAsync(string mvid, string methodFullName, InjectionRequest request, CancellationToken cancellationToken = default);
    Task<ModificationResult> AddTypeAsync(string mvid, TypeCreationRequest request, CancellationToken cancellationToken = default);
    Task<ModificationResult> SaveAssemblyAsync(string mvid, string outputPath, CancellationToken cancellationToken = default);
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
/// 程序集信息
/// </summary>
public record AssemblyInfo
{
    public required string Mvid { get; init; }
    public required string Name { get; init; }
    public required string Path { get; init; }
    public bool IsDefault { get; init; }
}
