using DotNetMcp.Backend.Services;
using DotNetMcp.Backend.Core.Context;
using DotNetMcp.Backend.Core.Analysis;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Server.Backend;

/// <summary>
/// 本地后端 - 直接调用 Core 服务
/// </summary>
public class LocalBackend : IBackend
{
    private readonly IAssemblyManager _assemblyManager;
    private readonly AnalysisService _analysisService;
    private readonly ModificationService _modificationService;
    private readonly ILogger<LocalBackend> _logger;
    private bool _isHealthy = true;
    private DateTime? _lastHealthCheck;

    public LocalBackend(
        IAssemblyManager assemblyManager,
        AnalysisService analysisService,
        ModificationService modificationService,
        ILogger<LocalBackend> logger)
    {
        _assemblyManager = assemblyManager;
        _analysisService = analysisService;
        _modificationService = modificationService;
        _logger = logger;
    }

    public string Id => "local";
    public string Name => "Local Backend";
    public BackendType Type => BackendType.Local;
    public bool IsHealthy => _isHealthy;
    public DateTime? LastHealthCheck => _lastHealthCheck;

    public Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        _isHealthy = true;
        _lastHealthCheck = DateTime.UtcNow;
        return Task.FromResult(true);
    }

    #region 程序集操作

    public async Task<AssemblyLoadResult> LoadAssemblyAsync(string path, IEnumerable<string>? searchPaths = null, CancellationToken cancellationToken = default)
    {
        return await _assemblyManager.LoadAsync(path, searchPaths, cancellationToken);
    }

    public Task<bool> UnloadAssemblyAsync(string mvid, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_assemblyManager.Unload(mvid));
    }

    public Task<IReadOnlyList<AssemblyInfo>> ListAssembliesAsync(CancellationToken cancellationToken = default)
    {
        var assemblies = _assemblyManager.GetAll();
        var defaultMvid = _assemblyManager.DefaultMvid;

        var result = assemblies.Select(ctx => new AssemblyInfo
        {
            Mvid = ctx.Mvid.ToString(),
            Name = ctx.Name ?? "Unknown",
            Path = ctx.AssemblyPath ?? "",
            IsDefault = ctx.Mvid.ToString() == defaultMvid
        }).ToList().AsReadOnly();

        return Task.FromResult<IReadOnlyList<AssemblyInfo>>(result);
    }

    #endregion

    #region 分析操作

    public Task<DecompileResult> DecompileTypeAsync(string mvid, string typeName, string language = "csharp", CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return Task.FromResult(DecompileResult.Failure($"Assembly '{mvid}' not found"));
        }

        return Task.FromResult(_analysisService.DecompileType(context, typeName, language));
    }

    public Task<DecompileResult> DecompileMethodAsync(string mvid, string typeName, string methodName, string language = "csharp", CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return Task.FromResult(DecompileResult.Failure($"Assembly '{mvid}' not found"));
        }

        return Task.FromResult(_analysisService.DecompileMethod(context, typeName, methodName, language));
    }

    public Task<SearchTypesResult> SearchTypesAsync(string mvid, string keyword, string? namespaceFilter = null, int limit = 50, CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return Task.FromResult(new SearchTypesResult { IsSuccess = false, ErrorMessage = $"Assembly '{mvid}' not found", Types = new List<TypeSummary>() });
        }

        return Task.FromResult(_analysisService.SearchTypes(context, keyword, namespaceFilter, limit));
    }

    public Task<SearchStringsResult> SearchStringsAsync(string mvid, string query, string mode = "contains", int limit = 50, CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return Task.FromResult(new SearchStringsResult { IsSuccess = false, ErrorMessage = $"Assembly '{mvid}' not found", Matches = new List<StringMatch>() });
        }

        return Task.FromResult(_analysisService.SearchStrings(context, query, mode, limit));
    }

    public Task<XRefResult> FindReferencesToTypeAsync(string mvid, string typeName, int limit = 50, CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return Task.FromResult(new XRefResult { IsSuccess = false, ErrorMessage = $"Assembly '{mvid}' not found", References = new List<CrossReference>() });
        }

        return Task.FromResult(_analysisService.FindReferencesToType(context, typeName, limit));
    }

    public Task<XRefResult> FindCallsToMethodAsync(string mvid, string typeName, string methodName, int limit = 50, CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return Task.FromResult(new XRefResult { IsSuccess = false, ErrorMessage = $"Assembly '{mvid}' not found", References = new List<CrossReference>() });
        }

        return Task.FromResult(_analysisService.FindCallsToMethod(context, typeName, methodName, limit));
    }

    public Task<CallGraphResult> BuildCallGraphAsync(string mvid, string typeName, string methodName, string direction = "callees", int maxDepth = 3, int maxNodes = 100, CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return Task.FromResult(new CallGraphResult { IsSuccess = false, ErrorMessage = $"Assembly '{mvid}' not found" });
        }

        return Task.FromResult(_analysisService.BuildCallGraph(context, typeName, methodName, direction, maxDepth, maxNodes));
    }

    public Task<CFGResult> BuildControlFlowGraphAsync(string mvid, string typeName, string methodName, bool includeIL = false, CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return Task.FromResult(new CFGResult { IsSuccess = false, ErrorMessage = $"Assembly '{mvid}' not found" });
        }

        return Task.FromResult(_analysisService.BuildControlFlowGraph(context, typeName, methodName, includeIL));
    }

    #endregion

    #region 修改操作

    public Task<ModificationResult> InjectAtEntryAsync(string mvid, string methodFullName, InjectionRequest request, CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return Task.FromResult(ModificationResult.Failure("ASSEMBLY_NOT_FOUND", $"Assembly '{mvid}' not found"));
        }

        return Task.FromResult(_modificationService.InjectAtEntry(context, methodFullName, request));
    }

    public Task<ModificationResult> ReplaceMethodBodyAsync(string mvid, string methodFullName, InjectionRequest request, CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return Task.FromResult(ModificationResult.Failure("ASSEMBLY_NOT_FOUND", $"Assembly '{mvid}' not found"));
        }

        return Task.FromResult(_modificationService.ReplaceMethodBody(context, methodFullName, request));
    }

    public Task<ModificationResult> AddTypeAsync(string mvid, TypeCreationRequest request, CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return Task.FromResult(ModificationResult.Failure("ASSEMBLY_NOT_FOUND", $"Assembly '{mvid}' not found"));
        }

        return Task.FromResult(_modificationService.AddType(context, request));
    }

    public Task<ModificationResult> SaveAssemblyAsync(string mvid, string outputPath, CancellationToken cancellationToken = default)
    {
        var context = GetContext(mvid);
        if (context == null)
        {
            return Task.FromResult(ModificationResult.Failure("ASSEMBLY_NOT_FOUND", $"Assembly '{mvid}' not found"));
        }

        return Task.FromResult(_modificationService.SaveAssembly(context, outputPath));
    }

    #endregion

    #region Helpers

    private AssemblyContext? GetContext(string? mvid)
    {
        return _assemblyManager.Get(mvid);
    }

    #endregion
}
