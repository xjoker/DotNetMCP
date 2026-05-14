using System.Net.Http.Json;
using System.Text.Json;
using DotNetMcp.Backend.Services;
using DotNetMcp.Backend.Core.Analysis;
using DotNetMcp.Backend.Core.Context;
using RoslynPatchResult = DotNetMcp.Backend.Core.Modification.RoslynPatchResult;
using ModificationResult = DotNetMcp.Backend.Services.ModificationResult;

namespace DotNetMcp.Server.Backend;

/// <summary>
/// 远程后端 - 通过 HTTP 代理转发到远程 Backend 服务
/// </summary>
public class RemoteBackend : IBackend
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RemoteBackend> _logger;
    private bool _isHealthy;
    private DateTime? _lastHealthCheck;

    public RemoteBackend(string id, string name, string endpoint, IHttpClientFactory httpClientFactory, ILogger<RemoteBackend> logger)
    {
        Id = id;
        Name = name;
        Endpoint = endpoint.TrimEnd('/');
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri(Endpoint);
        _logger = logger;
    }

    public string Id { get; }
    public string Name { get; }
    public string Endpoint { get; }
    public string? ApiKey { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public BackendType Type => BackendType.Remote;
    public bool IsHealthy => _isHealthy;
    public DateTime? LastHealthCheck => _lastHealthCheck;

    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            using var request = CreateRequest(HttpMethod.Get, "/instance/health");
            using var response = await _httpClient.SendAsync(request, cts.Token);
            _isHealthy = response.IsSuccessStatusCode;
            _lastHealthCheck = DateTime.UtcNow;

            if (!_isHealthy)
            {
                _logger.LogWarning("Health check failed for remote backend {Id}: {StatusCode}", Id, response.StatusCode);
            }

            return _isHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check exception for remote backend {Id}", Id);
            _isHealthy = false;
            _lastHealthCheck = DateTime.UtcNow;
            return false;
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, object? content = null)
    {
        var request = new HttpRequestMessage(method, path);

        if (ApiKey != null)
        {
            request.Headers.Add("X-API-Key", ApiKey);
        }

        if (content != null)
        {
            request.Content = JsonContent.Create(content);
        }

        return request;
    }

    private async Task<T?> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken) where T : class
    {
        using (request)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

                using var response = await _httpClient.SendAsync(request, cts.Token);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<T>(cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Remote backend request failed: {Method} {Path}", request.Method, request.RequestUri);
                throw;
            }
        }
    }

    #region 程序集操作

    public async Task<AssemblyLoadResult> LoadAssemblyAsync(string path, IEnumerable<string>? searchPaths = null, CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(HttpMethod.Post, "/assembly/load", new { path, search_paths = searchPaths?.ToList() });
        try
        {
            var envelope = await SendAsync<RemoteEnvelope<RemoteAssemblyLoadData>>(request, cancellationToken);
            if (envelope is null)
            {
                return AssemblyLoadResult.Failure(AssemblyLoadErrorCode.Unknown, "Empty response from remote backend");
            }
            if (envelope.Success && envelope.Data?.Mvid is not null && envelope.Data?.Name is not null)
            {
                return AssemblyLoadResult.SuccessRemote(envelope.Data.Mvid, envelope.Data.Name);
            }
            return AssemblyLoadResult.Failure(AssemblyLoadErrorCode.Unknown, envelope.Message ?? "Remote load failed");
        }
        catch (Exception ex)
        {
            return AssemblyLoadResult.Failure(AssemblyLoadErrorCode.Unknown, ex.Message);
        }
    }

    public async Task<bool> UnloadAssemblyAsync(string mvid, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"/instance/{mvid}");
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

            using var response = await _httpClient.SendAsync(request, cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<AssemblyListItem>> ListAssembliesAsync(CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(HttpMethod.Get, "/instance/list");
        try
        {
            var envelope = await SendAsync<RemoteEnvelope<RemoteAssemblyListData>>(request, cancellationToken);
            if (envelope?.Success != true || envelope.Data?.Instances is null)
            {
                return new List<AssemblyListItem>().AsReadOnly();
            }
            return envelope.Data.Instances.Select(a => new AssemblyListItem
            {
                Mvid = a.Mvid ?? string.Empty,
                Name = a.Name ?? string.Empty,
                Path = a.Path ?? string.Empty,
                IsDefault = a.IsDefault
            }).ToList().AsReadOnly();
        }
        catch
        {
            return new List<AssemblyListItem>().AsReadOnly();
        }
    }

    #endregion

    #region 分析操作

    public async Task<DecompileResult> DecompileTypeAsync(string mvid, string typeName, string language = "csharp", bool preferOriginalSource = false, CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(HttpMethod.Get, $"/analysis/type/{Uri.EscapeDataString(typeName)}/source?language={language}&mvid={mvid}&preferOriginalSource={preferOriginalSource}");
        try
        {
            var result = await SendAsync<DecompileResult>(request, cancellationToken);
            return result ?? DecompileResult.Failure("Empty response from remote backend");
        }
        catch (Exception ex)
        {
            return DecompileResult.Failure(ex.Message);
        }
    }

    public async Task<DecompileResult> DecompileMethodAsync(string mvid, string typeName, string methodName, string language = "csharp", bool preferOriginalSource = false, CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(HttpMethod.Get, $"/analysis/type/{Uri.EscapeDataString(typeName)}/method/{Uri.EscapeDataString(methodName)}?language={language}&mvid={mvid}&preferOriginalSource={preferOriginalSource}");
        try
        {
            var result = await SendAsync<DecompileResult>(request, cancellationToken);
            return result ?? DecompileResult.Failure("Empty response from remote backend");
        }
        catch (Exception ex)
        {
            return DecompileResult.Failure(ex.Message);
        }
    }

    public async Task<SearchTypesResult> SearchTypesAsync(string mvid, string keyword, string? namespaceFilter = null, int limit = 50, CancellationToken cancellationToken = default)
    {
        var url = $"/analysis/search/types?keyword={Uri.EscapeDataString(keyword)}&limit={limit}&mvid={mvid}";
        if (namespaceFilter != null) url += $"&namespace={Uri.EscapeDataString(namespaceFilter)}";

        var request = CreateRequest(HttpMethod.Get, url);
        try
        {
            var result = await SendAsync<SearchTypesResult>(request, cancellationToken);
            return result ?? new SearchTypesResult { IsSuccess = false, ErrorMessage = "Empty response from remote backend", Types = new List<TypeSummary>() };
        }
        catch (Exception ex)
        {
            return new SearchTypesResult { IsSuccess = false, ErrorMessage = ex.Message, Types = new List<TypeSummary>() };
        }
    }

    public async Task<SearchStringsResult> SearchStringsAsync(string mvid, string query, string mode = "contains", int limit = 50, CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(HttpMethod.Get, $"/analysis/search/strings?query={Uri.EscapeDataString(query)}&mode={mode}&limit={limit}&mvid={mvid}");
        try
        {
            var result = await SendAsync<SearchStringsResult>(request, cancellationToken);
            return result ?? new SearchStringsResult { IsSuccess = false, ErrorMessage = "Empty response from remote backend", Matches = new List<StringMatch>() };
        }
        catch (Exception ex)
        {
            return new SearchStringsResult { IsSuccess = false, ErrorMessage = ex.Message, Matches = new List<StringMatch>() };
        }
    }

    public async Task<XRefResult> FindReferencesToTypeAsync(string mvid, string typeName, int limit = 50, CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(HttpMethod.Get, $"/analysis/xrefs/type/{Uri.EscapeDataString(typeName)}?limit={limit}&mvid={mvid}");
        try
        {
            var result = await SendAsync<XRefResult>(request, cancellationToken);
            return result ?? new XRefResult { IsSuccess = false, ErrorMessage = "Empty response from remote backend", References = new List<CrossReference>() };
        }
        catch (Exception ex)
        {
            return new XRefResult { IsSuccess = false, ErrorMessage = ex.Message, References = new List<CrossReference>() };
        }
    }

    public async Task<XRefResult> FindCallsToMethodAsync(string mvid, string typeName, string methodName, int limit = 50, CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(HttpMethod.Get, $"/analysis/xrefs/method/{Uri.EscapeDataString(typeName)}/{Uri.EscapeDataString(methodName)}?limit={limit}&mvid={mvid}");
        try
        {
            var result = await SendAsync<XRefResult>(request, cancellationToken);
            return result ?? new XRefResult { IsSuccess = false, ErrorMessage = "Empty response from remote backend", References = new List<CrossReference>() };
        }
        catch (Exception ex)
        {
            return new XRefResult { IsSuccess = false, ErrorMessage = ex.Message, References = new List<CrossReference>() };
        }
    }

    public async Task<CallGraphResult> BuildCallGraphAsync(string mvid, string typeName, string methodName, string direction = "callees", int maxDepth = 3, int maxNodes = 100, CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(HttpMethod.Get, $"/analysis/callgraph/{Uri.EscapeDataString(typeName)}/{Uri.EscapeDataString(methodName)}?direction={direction}&max_depth={maxDepth}&max_nodes={maxNodes}&mvid={mvid}");
        try
        {
            var result = await SendAsync<CallGraphResult>(request, cancellationToken);
            return result ?? new CallGraphResult { IsSuccess = false, ErrorMessage = "Empty response from remote backend" };
        }
        catch (Exception ex)
        {
            return new CallGraphResult { IsSuccess = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<CFGResult> BuildControlFlowGraphAsync(string mvid, string typeName, string methodName, bool includeIL = false, CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(HttpMethod.Get, $"/analysis/cfg/{Uri.EscapeDataString(typeName)}/{Uri.EscapeDataString(methodName)}?include_il={includeIL}&mvid={mvid}");
        try
        {
            var result = await SendAsync<CFGResult>(request, cancellationToken);
            return result ?? new CFGResult { IsSuccess = false, ErrorMessage = "Empty response from remote backend" };
        }
        catch (Exception ex)
        {
            return new CFGResult { IsSuccess = false, ErrorMessage = ex.Message };
        }
    }

    #endregion

    #region 批量操作

    public async Task<BatchDecompileResult> BatchDecompileAsync(string mvid, string[] memberKeys, int maxTotalChars = 200000, CancellationToken cancellationToken = default)
    {
        var httpRequest = CreateRequest(HttpMethod.Post, $"/analysis/batch-decompile?mvid={mvid}", new { memberKeys, maxTotalChars });
        try
        {
            var result = await SendAsync<BatchDecompileResult>(httpRequest, cancellationToken);
            return result ?? BatchDecompileResult.Failure("Empty response from remote backend");
        }
        catch (Exception ex)
        {
            return BatchDecompileResult.Failure(ex.Message);
        }
    }

    public async Task<ChunkingPlanResult> PlanChunkingAsync(string mvid, string typeName, string? methodName = null, int targetChunkSize = 6000, int overlap = 2, CancellationToken cancellationToken = default)
    {
        var url = $"/analysis/plan-chunking?typeName={Uri.EscapeDataString(typeName)}&targetChunkSize={targetChunkSize}&overlap={overlap}&mvid={mvid}";
        if (methodName != null) url += $"&methodName={Uri.EscapeDataString(methodName)}";

        var httpRequest = CreateRequest(HttpMethod.Get, url);
        try
        {
            var result = await SendAsync<ChunkingPlanResult>(httpRequest, cancellationToken);
            return result ?? ChunkingPlanResult.Failure("Empty response from remote backend");
        }
        catch (Exception ex)
        {
            return ChunkingPlanResult.Failure(ex.Message);
        }
    }

    public async Task<TypeOutlineResult> GetTypeOutlineAsync(string mvid, string typeName, CancellationToken cancellationToken = default)
    {
        var httpRequest = CreateRequest(HttpMethod.Get, $"/analysis/outline/{Uri.EscapeDataString(typeName)}?mvid={mvid}");
        try
        {
            var result = await SendAsync<TypeOutlineResult>(httpRequest, cancellationToken);
            return result ?? TypeOutlineResult.Failure("Empty response from remote backend");
        }
        catch (Exception ex)
        {
            return TypeOutlineResult.Failure(ex.Message);
        }
    }

    public async Task<PatchSkeletonResult> GeneratePatchSkeletonAsync(string mvid, string typeName, string methodName, string[] patchKinds, CancellationToken cancellationToken = default)
    {
        var httpRequest = CreateRequest(HttpMethod.Post, $"/analysis/patch-skeleton?mvid={mvid}", new { typeName, methodName, patchKinds });
        try
        {
            var result = await SendAsync<PatchSkeletonResult>(httpRequest, cancellationToken);
            return result ?? PatchSkeletonResult.Failure("Empty response from remote backend");
        }
        catch (Exception ex)
        {
            return PatchSkeletonResult.Failure(ex.Message);
        }
    }

    public async Task<CompareAssembliesResult> CompareAssembliesAsync(string leftMvid, string rightMvid, string? namespaceFilter = null, bool includeUnchanged = false, CancellationToken cancellationToken = default)
    {
        var url = $"/analysis/compare?leftMvid={Uri.EscapeDataString(leftMvid)}&rightMvid={Uri.EscapeDataString(rightMvid)}&includeUnchanged={includeUnchanged}";
        if (namespaceFilter != null) url += $"&namespaceFilter={Uri.EscapeDataString(namespaceFilter)}";

        var httpRequest = CreateRequest(HttpMethod.Get, url);
        try
        {
            var result = await SendAsync<CompareAssembliesResult>(httpRequest, cancellationToken);
            return result ?? CompareAssembliesResult.Failure("Empty response from remote backend");
        }
        catch (Exception ex)
        {
            return CompareAssembliesResult.Failure(ex.Message);
        }
    }

    #endregion

    #region 图分析

    public async Task<DependencyGraphResult> BuildDependencyGraphAsync(string mvid, string level, string? rootType, int maxDepth, CancellationToken cancellationToken = default)
    {
        var url = $"/analysis/dependencies?level={Uri.EscapeDataString(level)}&max_depth={maxDepth}&mvid={Uri.EscapeDataString(mvid)}";
        if (rootType != null) url += $"&root_type={Uri.EscapeDataString(rootType)}";

        var httpRequest = CreateRequest(HttpMethod.Get, url);
        try
        {
            var result = await SendAsync<DependencyGraphResult>(httpRequest, cancellationToken);
            return result ?? new DependencyGraphResult { IsSuccess = false, ErrorMessage = "Empty response from remote backend" };
        }
        catch (Exception ex)
        {
            return new DependencyGraphResult { IsSuccess = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<PatternDetectionServiceResult> DetectPatternsAsync(string mvid, string? typeName, CancellationToken cancellationToken = default)
    {
        var url = $"/analysis/patterns?mvid={Uri.EscapeDataString(mvid)}";
        if (typeName != null) url += $"&type_name={Uri.EscapeDataString(typeName)}";

        var httpRequest = CreateRequest(HttpMethod.Get, url);
        try
        {
            var result = await SendAsync<PatternDetectionServiceResult>(httpRequest, cancellationToken);
            return result ?? new PatternDetectionServiceResult { IsSuccess = false, ErrorMessage = "Empty response from remote backend" };
        }
        catch (Exception ex)
        {
            return new PatternDetectionServiceResult { IsSuccess = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<ObfuscationDetectionServiceResult> DetectObfuscationAsync(string mvid, CancellationToken cancellationToken = default)
    {
        var httpRequest = CreateRequest(HttpMethod.Get, $"/analysis/obfuscation?mvid={Uri.EscapeDataString(mvid)}");
        try
        {
            var result = await SendAsync<ObfuscationDetectionServiceResult>(httpRequest, cancellationToken);
            return result ?? new ObfuscationDetectionServiceResult { IsSuccess = false, ErrorMessage = "Empty response from remote backend" };
        }
        catch (Exception ex)
        {
            return new ObfuscationDetectionServiceResult { IsSuccess = false, ErrorMessage = ex.Message };
        }
    }

    #endregion

    #region 增强搜索

    public async Task<EnhancedSearchResult> EnhancedSearchAsync(string mvid, string query, string mode, string? namespaceFilter, int limit, CancellationToken cancellationToken = default)
    {
        var url = $"/analysis/enhanced-search?query={Uri.EscapeDataString(query)}&mode={Uri.EscapeDataString(mode)}&limit={limit}&mvid={Uri.EscapeDataString(mvid)}";
        if (namespaceFilter != null) url += $"&namespace={Uri.EscapeDataString(namespaceFilter)}";

        var httpRequest = CreateRequest(HttpMethod.Get, url);
        try
        {
            var result = await SendAsync<EnhancedSearchResult>(httpRequest, cancellationToken);
            return result ?? new EnhancedSearchResult
            {
                Items = Array.Empty<SearchResultItem>(),
                TotalCount = 0,
                HasMore = false,
                SearchDuration = TimeSpan.Zero,
                Query = query,
                Mode = SearchMode.TypeAndMember
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EnhancedSearchAsync failed for query '{Query}'", query);
            return new EnhancedSearchResult
            {
                Items = Array.Empty<SearchResultItem>(),
                TotalCount = 0,
                HasMore = false,
                SearchDuration = TimeSpan.Zero,
                Query = query,
                Mode = SearchMode.TypeAndMember
            };
        }
    }

    #endregion

    #region 修改操作

    public async Task<ModificationResult> InjectAtEntryAsync(string mvid, string methodFullName, InjectionRequest request, CancellationToken cancellationToken = default)
    {
        var httpRequest = CreateRequest(HttpMethod.Post, "/modification/inject/entry", new { mvid, methodFullName, instructions = request.Instructions });
        try
        {
            var result = await SendAsync<ModificationResult>(httpRequest, cancellationToken);
            return result ?? ModificationResult.Failure("REMOTE_ERROR", "Empty response from remote backend");
        }
        catch (Exception ex)
        {
            return ModificationResult.Failure("REMOTE_ERROR", ex.Message);
        }
    }

    public async Task<ModificationResult> ReplaceMethodBodyAsync(string mvid, string methodFullName, InjectionRequest request, CancellationToken cancellationToken = default)
    {
        var httpRequest = CreateRequest(HttpMethod.Post, "/modification/replace/body", new { mvid, methodFullName, instructions = request.Instructions });
        try
        {
            var result = await SendAsync<ModificationResult>(httpRequest, cancellationToken);
            return result ?? ModificationResult.Failure("REMOTE_ERROR", "Empty response from remote backend");
        }
        catch (Exception ex)
        {
            return ModificationResult.Failure("REMOTE_ERROR", ex.Message);
        }
    }

    public async Task<ModificationResult> AddTypeAsync(string mvid, TypeCreationRequest request, CancellationToken cancellationToken = default)
    {
        var httpRequest = CreateRequest(HttpMethod.Post, "/modification/type/add", new { mvid, request.Namespace, request.Name, request.Kind });
        try
        {
            var result = await SendAsync<ModificationResult>(httpRequest, cancellationToken);
            return result ?? ModificationResult.Failure("REMOTE_ERROR", "Empty response from remote backend");
        }
        catch (Exception ex)
        {
            return ModificationResult.Failure("REMOTE_ERROR", ex.Message);
        }
    }

    public async Task<ModificationResult> SaveAssemblyAsync(string mvid, string outputPath, CancellationToken cancellationToken = default)
    {
        var httpRequest = CreateRequest(HttpMethod.Post, "/modification/save", new { mvid, outputPath });
        try
        {
            var result = await SendAsync<ModificationResult>(httpRequest, cancellationToken);
            return result ?? ModificationResult.Failure("REMOTE_ERROR", "Empty response from remote backend");
        }
        catch (Exception ex)
        {
            return ModificationResult.Failure("REMOTE_ERROR", ex.Message);
        }
    }

    public async Task<RoslynPatchResult> ReplaceMethodBodyWithCSharpAsync(
        string mvid,
        string methodFullName,
        string csharpBody,
        string[]? usings,
        bool allowUnsafe,
        CancellationToken cancellationToken = default)
    {
        var httpRequest = CreateRequest(HttpMethod.Post, "/modification/csharp", new
        {
            mvid,
            methodFullName,
            csharpBody,
            usings,
            allowUnsafe
        });
        try
        {
            var result = await SendAsync<RoslynPatchResult>(httpRequest, cancellationToken);
            return result ?? RoslynPatchResult.Failure("Empty response from remote backend");
        }
        catch (Exception ex)
        {
            return RoslynPatchResult.Failure(ex.Message);
        }
    }

    #endregion

    #region 继承分析

    public async Task<InheritanceResult> FindBaseTypesAsync(string mvid, string typeName, bool includeInterfaces = true, CancellationToken cancellationToken = default)
    {
        var httpRequest = CreateRequest(HttpMethod.Get, $"/analysis/inheritance/base-types/{Uri.EscapeDataString(typeName)}?includeInterfaces={includeInterfaces}&mvid={mvid}");
        try
        {
            var result = await SendAsync<InheritanceResult>(httpRequest, cancellationToken);
            return result ?? InheritanceResult.Failure("Empty response from remote backend");
        }
        catch (Exception ex)
        {
            return InheritanceResult.Failure(ex.Message);
        }
    }

    public async Task<InheritanceResult> FindDerivedTypesAsync(string mvid, string typeName, bool directOnly = false, CancellationToken cancellationToken = default)
    {
        var httpRequest = CreateRequest(HttpMethod.Get, $"/analysis/inheritance/derived-types/{Uri.EscapeDataString(typeName)}?directOnly={directOnly}&mvid={mvid}");
        try
        {
            var result = await SendAsync<InheritanceResult>(httpRequest, cancellationToken);
            return result ?? InheritanceResult.Failure("Empty response from remote backend");
        }
        catch (Exception ex)
        {
            return InheritanceResult.Failure(ex.Message);
        }
    }

    public async Task<InheritanceResult> GetImplementationsAsync(string mvid, string interfaceTypeName, CancellationToken cancellationToken = default)
    {
        var httpRequest = CreateRequest(HttpMethod.Get, $"/analysis/inheritance/implementations/{Uri.EscapeDataString(interfaceTypeName)}?mvid={mvid}");
        try
        {
            var result = await SendAsync<InheritanceResult>(httpRequest, cancellationToken);
            return result ?? InheritanceResult.Failure("Empty response from remote backend");
        }
        catch (Exception ex)
        {
            return InheritanceResult.Failure(ex.Message);
        }
    }

    public async Task<MethodInheritanceResult> GetOverridesAsync(string mvid, string typeName, string methodName, CancellationToken cancellationToken = default)
    {
        var httpRequest = CreateRequest(HttpMethod.Get, $"/analysis/inheritance/overrides/{Uri.EscapeDataString(typeName)}/{Uri.EscapeDataString(methodName)}?mvid={mvid}");
        try
        {
            var result = await SendAsync<MethodInheritanceResult>(httpRequest, cancellationToken);
            return result ?? MethodInheritanceResult.Failure("Empty response from remote backend");
        }
        catch (Exception ex)
        {
            return MethodInheritanceResult.Failure(ex.Message);
        }
    }

    public async Task<MethodInheritanceResult> GetOverloadsAsync(string mvid, string typeName, string methodName, CancellationToken cancellationToken = default)
    {
        var httpRequest = CreateRequest(HttpMethod.Get, $"/analysis/inheritance/overloads/{Uri.EscapeDataString(typeName)}/{Uri.EscapeDataString(methodName)}?mvid={mvid}");
        try
        {
            var result = await SendAsync<MethodInheritanceResult>(httpRequest, cancellationToken);
            return result ?? MethodInheritanceResult.Failure("Empty response from remote backend");
        }
        catch (Exception ex)
        {
            return MethodInheritanceResult.Failure(ex.Message);
        }
    }

    #endregion

    #region Alias 管理

    public async Task<AliasOperationResult> RegisterAssemblyAliasAsync(string alias, string mvid, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        var httpRequest = CreateRequest(HttpMethod.Post, $"/instance/alias", new { alias, mvid, overwrite });
        try
        {
            var envelope = await SendAsync<RemoteEnvelope<AliasOperationResult>>(httpRequest, cancellationToken);
            if (envelope?.Success == true)
                return AliasOperationResult.Success(alias, mvid);
            return AliasOperationResult.Failure(envelope?.Message ?? "Remote alias register failed");
        }
        catch (Exception ex)
        {
            return AliasOperationResult.Failure(ex.Message);
        }
    }

    public async Task<AliasOperationResult> UnregisterAssemblyAliasAsync(string alias, CancellationToken cancellationToken = default)
    {
        var httpRequest = CreateRequest(HttpMethod.Delete, $"/instance/alias/{Uri.EscapeDataString(alias)}");
        try
        {
            var envelope = await SendAsync<RemoteEnvelope<object>>(httpRequest, cancellationToken);
            if (envelope?.Success == true)
                return AliasOperationResult.Success(alias);
            return AliasOperationResult.Failure(envelope?.Message ?? "Remote alias unregister failed");
        }
        catch (Exception ex)
        {
            return AliasOperationResult.Failure(ex.Message);
        }
    }

    public async Task<ListAliasesResult> ListAssemblyAliasesAsync(CancellationToken cancellationToken = default)
    {
        var httpRequest = CreateRequest(HttpMethod.Get, "/instance/aliases");
        try
        {
            var envelope = await SendAsync<RemoteEnvelope<RemoteAliasListData>>(httpRequest, cancellationToken);
            if (envelope?.Success == true && envelope.Data?.Aliases != null)
            {
                var aliases = envelope.Data.Aliases.Select(a => new AliasInfoDto
                {
                    Alias = a.Alias ?? string.Empty,
                    Mvid = a.Mvid ?? string.Empty
                }).ToList();
                return ListAliasesResult.Success(aliases);
            }
            return ListAliasesResult.Failure(envelope?.Message ?? "Remote list aliases failed");
        }
        catch (Exception ex)
        {
            return ListAliasesResult.Failure(ex.Message);
        }
    }

    public async Task<RestorePersistedResult> RestorePersistedAssembliesAsync(CancellationToken cancellationToken = default)
    {
        var httpRequest = CreateRequest(HttpMethod.Post, "/instance/alias/restore");
        try
        {
            var envelope = await SendAsync<RemoteEnvelope<RemoteRestoreData>>(httpRequest, cancellationToken);
            if (envelope?.Success == true)
                return RestorePersistedResult.Success(envelope.Data?.RestoredCount ?? 0);
            return RestorePersistedResult.Failure(envelope?.Message ?? "Remote restore failed");
        }
        catch (Exception ex)
        {
            return RestorePersistedResult.Failure(ex.Message);
        }
    }

    #endregion

    #region 索引预热

    public async Task<WarmIndexResult> WarmIndexAsync(string mvid, bool typeIndex = true, bool memberIndex = true, int? maxSeconds = null, CancellationToken cancellationToken = default)
    {
        var url = $"/instance/warm-index?mvid={Uri.EscapeDataString(mvid)}&typeIndex={typeIndex}&memberIndex={memberIndex}";
        if (maxSeconds.HasValue) url += $"&maxSeconds={maxSeconds.Value}";
        var httpRequest = CreateRequest(HttpMethod.Post, url);
        try
        {
            var result = await SendAsync<WarmIndexResult>(httpRequest, cancellationToken);
            return result ?? new WarmIndexResult { IsSuccess = false, ErrorMessage = "Empty response from remote backend" };
        }
        catch (Exception ex)
        {
            return new WarmIndexResult { IsSuccess = false, ErrorMessage = ex.Message };
        }
    }

    #endregion
}

#region Remote Response Types

/// <summary>
/// Backend 统一 envelope 格式：{ success, data, error_code, message }
/// </summary>
internal class RemoteEnvelope<T>
{
    [System.Text.Json.Serialization.JsonPropertyName("success")]
    public bool Success { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("data")]
    public T? Data { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("error_code")]
    public string? ErrorCode { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("message")]
    public string? Message { get; set; }
}

internal class RemoteAssemblyLoadData
{
    [System.Text.Json.Serialization.JsonPropertyName("mvid")]
    public string? Mvid { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string? Name { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("version")]
    public string? Version { get; set; }
}

internal class RemoteAssemblyListData
{
    [System.Text.Json.Serialization.JsonPropertyName("instances")]
    public List<RemoteAssemblyInfo>? Instances { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("count")]
    public int Count { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("default_instance")]
    public string? DefaultInstance { get; set; }
}

internal class RemoteAssemblyInfo
{
    [System.Text.Json.Serialization.JsonPropertyName("mvid")]
    public string? Mvid { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string? Name { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("path")]
    public string? Path { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("is_default")]
    public bool IsDefault { get; set; }
}

internal class RemoteAliasListData
{
    [System.Text.Json.Serialization.JsonPropertyName("aliases")]
    public List<RemoteAliasInfo>? Aliases { get; set; }
}

internal class RemoteAliasInfo
{
    [System.Text.Json.Serialization.JsonPropertyName("alias")]
    public string? Alias { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("mvid")]
    public string? Mvid { get; set; }
}

internal class RemoteRestoreData
{
    [System.Text.Json.Serialization.JsonPropertyName("restored_count")]
    public int RestoredCount { get; set; }
}

#endregion
