using System.Net.Http.Json;
using System.Text.Json;
using DotNetMcp.Backend.Services;
using DotNetMcp.Backend.Core.Analysis;
using DotNetMcp.Backend.Core.Context;

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

            var response = await _httpClient.GetAsync("/api/instance/health", cts.Token);
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
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

            var response = await _httpClient.SendAsync(request, cts.Token);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<T>(cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Remote backend request failed: {Method} {Path}", request.Method, request.RequestUri);
            throw;
        }
    }

    #region 程序集操作

    public async Task<AssemblyLoadResult> LoadAssemblyAsync(string path, IEnumerable<string>? searchPaths = null, CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(HttpMethod.Post, "/api/assembly/load", new { path, search_paths = searchPaths?.ToList() });
        try
        {
            var response = await SendAsync<RemoteAssemblyLoadResponse>(request, cancellationToken);
            if (response is null)
            {
                return AssemblyLoadResult.Failure(AssemblyLoadErrorCode.Unknown, "Empty response from remote backend");
            }
            if (response.Success && response.Mvid is not null && response.Name is not null)
            {
                return AssemblyLoadResult.SuccessRemote(response.Mvid, response.Name);
            }
            return AssemblyLoadResult.Failure(AssemblyLoadErrorCode.Unknown, response.Error ?? "Remote load failed");
        }
        catch (Exception ex)
        {
            return AssemblyLoadResult.Failure(AssemblyLoadErrorCode.Unknown, ex.Message);
        }
    }

    public async Task<bool> UnloadAssemblyAsync(string mvid, CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(HttpMethod.Delete, $"/api/assembly/{mvid}");
        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<AssemblyInfo>> ListAssembliesAsync(CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(HttpMethod.Get, "/api/assembly");
        try
        {
            var response = await SendAsync<RemoteAssemblyListResponse>(request, cancellationToken);
            if (response?.Assemblies is null)
            {
                return new List<AssemblyInfo>().AsReadOnly();
            }
            return response.Assemblies.Select(a => new AssemblyInfo
            {
                Mvid = a.Mvid,
                Name = a.Name,
                Path = a.Path,
                IsDefault = a.IsDefault
            }).ToList().AsReadOnly();
        }
        catch
        {
            return new List<AssemblyInfo>().AsReadOnly();
        }
    }

    #endregion

    #region 分析操作

    public async Task<DecompileResult> DecompileTypeAsync(string mvid, string typeName, string language = "csharp", CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(HttpMethod.Get, $"/api/analysis/{mvid}/decompile/type/{Uri.EscapeDataString(typeName)}?language={language}");
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

    public async Task<DecompileResult> DecompileMethodAsync(string mvid, string typeName, string methodName, string language = "csharp", CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(HttpMethod.Get, $"/api/analysis/{mvid}/decompile/method/{Uri.EscapeDataString(typeName)}/{Uri.EscapeDataString(methodName)}?language={language}");
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
        var url = $"/api/analysis/{mvid}/search/types?keyword={Uri.EscapeDataString(keyword)}&limit={limit}";
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
        var request = CreateRequest(HttpMethod.Get, $"/api/analysis/{mvid}/search/strings?query={Uri.EscapeDataString(query)}&mode={mode}&limit={limit}");
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
        var request = CreateRequest(HttpMethod.Get, $"/api/analysis/{mvid}/xref/type/{Uri.EscapeDataString(typeName)}?limit={limit}");
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
        var request = CreateRequest(HttpMethod.Get, $"/api/analysis/{mvid}/xref/method/{Uri.EscapeDataString(typeName)}/{Uri.EscapeDataString(methodName)}?limit={limit}");
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
        var request = CreateRequest(HttpMethod.Get, $"/api/analysis/{mvid}/callgraph/{Uri.EscapeDataString(typeName)}/{Uri.EscapeDataString(methodName)}?direction={direction}&maxDepth={maxDepth}&maxNodes={maxNodes}");
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
        var request = CreateRequest(HttpMethod.Get, $"/api/analysis/{mvid}/cfg/{Uri.EscapeDataString(typeName)}/{Uri.EscapeDataString(methodName)}?includeIL={includeIL}");
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
        var httpRequest = CreateRequest(HttpMethod.Post, $"/api/analysis/{mvid}/batch-decompile", new { memberKeys, maxTotalChars });
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
        var url = $"/api/analysis/{mvid}/plan-chunking?typeName={Uri.EscapeDataString(typeName)}&targetChunkSize={targetChunkSize}&overlap={overlap}";
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
        var httpRequest = CreateRequest(HttpMethod.Get, $"/api/analysis/{mvid}/outline/{Uri.EscapeDataString(typeName)}");
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
        var httpRequest = CreateRequest(HttpMethod.Post, $"/api/analysis/{mvid}/patch-skeleton", new { typeName, methodName, patchKinds });
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
        var url = $"/api/analysis/compare?leftMvid={Uri.EscapeDataString(leftMvid)}&rightMvid={Uri.EscapeDataString(rightMvid)}&includeUnchanged={includeUnchanged}";
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

    #region 修改操作

    public async Task<ModificationResult> InjectAtEntryAsync(string mvid, string methodFullName, InjectionRequest request, CancellationToken cancellationToken = default)
    {
        var httpRequest = CreateRequest(HttpMethod.Post, $"/api/modification/{mvid}/inject/entry", new { method = methodFullName, instructions = request.Instructions });
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
        var httpRequest = CreateRequest(HttpMethod.Post, $"/api/modification/{mvid}/replace/body", new { method = methodFullName, instructions = request.Instructions });
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
        var httpRequest = CreateRequest(HttpMethod.Post, $"/api/modification/{mvid}/type", request);
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
        var httpRequest = CreateRequest(HttpMethod.Post, $"/api/modification/{mvid}/save", new { path = outputPath });
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

    #endregion
}

#region Remote Response Types

internal class RemoteAssemblyLoadResponse
{
    public bool Success { get; set; }
    public string? Mvid { get; set; }
    public string? Name { get; set; }
    public string? Error { get; set; }
}

internal class RemoteAssemblyListResponse
{
    public List<RemoteAssemblyInfo>? Assemblies { get; set; }
}

internal class RemoteAssemblyInfo
{
    public required string Mvid { get; set; }
    public required string Name { get; set; }
    public required string Path { get; set; }
    public bool IsDefault { get; set; }
}

#endregion
