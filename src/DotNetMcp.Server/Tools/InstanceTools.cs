using System.ComponentModel;
using ModelContextProtocol.Server;
using DotNetMcp.Server.Backend;

namespace DotNetMcp.Server.Tools;

/// <summary>
/// 后端实例管理工具
/// </summary>
[McpServerToolType]
public sealed class InstanceTools
{
    private readonly IBackendRegistry _registry;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RemoteBackend> _remoteBackendLogger;

    public InstanceTools(
        IBackendRegistry registry,
        IHttpClientFactory httpClientFactory,
        ILogger<RemoteBackend> remoteBackendLogger)
    {
        _registry = registry;
        _httpClientFactory = httpClientFactory;
        _remoteBackendLogger = remoteBackendLogger;
    }

    /// <summary>
    /// 列出所有已注册的后端
    /// </summary>
    [McpServerTool(Name = "list_backends"), Description("List all available backends (local and remote). Shows backend ID, name, type, health status, and which is default. The default backend is used when backendId is omitted in other tools.")]
    public ListBackendsResult ListBackends()
    {
        var backends = _registry.GetAll();
        var defaultId = _registry.DefaultBackendId;

        return new ListBackendsResult
        {
            Success = true,
            Backends = backends.Select(b => new BackendInfoDto
            {
                Id = b.Id,
                Name = b.Name,
                Type = b.Type.ToString(),
                IsHealthy = b.IsHealthy,
                IsDefault = b.Id == defaultId,
                LastHealthCheck = b.LastHealthCheck?.ToString("O")
            }).ToArray()
        };
    }

    /// <summary>
    /// 注册远程后端
    /// </summary>
    [McpServerTool(Name = "register_remote_backend"), Description("Connect to a remote DotNetMcp.Backend instance over HTTP. Use for distributed analysis, accessing remote assemblies, or load balancing. Validates connectivity before registering.")]
    public async Task<RegisterBackendResult> RegisterRemoteBackend(
        [Description("Unique identifier for this backend (e.g., 'remote-1', 'prod-server')")] string id,
        [Description("Human-readable display name (e.g., 'Production Analysis Server')")] string name,
        [Description("HTTP endpoint URL of the remote backend (e.g., 'http://server:5000')")] string endpoint,
        [Description("API key for authentication (if the remote backend requires it)")] string? apiKey = null,
        [Description("Connection timeout in seconds (default: 30)")] int timeoutSeconds = 30)
    {
        if (_registry.Get(id) != null)
        {
            return new RegisterBackendResult { Success = false, Error = $"Backend '{id}' already exists" };
        }

        var backend = new RemoteBackend(id, name, endpoint, _httpClientFactory, _remoteBackendLogger)
        {
            ApiKey = apiKey,
            TimeoutSeconds = timeoutSeconds
        };

        // 验证连接
        var healthy = await backend.CheckHealthAsync();
        if (!healthy)
        {
            return new RegisterBackendResult
            {
                Success = false,
                Error = $"Failed to connect to remote backend at {endpoint}"
            };
        }

        _registry.Register(backend);

        return new RegisterBackendResult
        {
            Success = true,
            BackendId = id,
            Message = $"Successfully registered remote backend '{name}'"
        };
    }

    /// <summary>
    /// 注销后端
    /// </summary>
    [McpServerTool(Name = "unregister_backend"), Description("Disconnect and remove a registered backend. Frees associated resources. Cannot unregister the built-in 'local' backend.")]
    public UnregisterBackendResult UnregisterBackend(
        [Description("Backend ID to unregister (cannot be 'local')")] string id)
    {
        if (id == "local")
        {
            return new UnregisterBackendResult { Success = false, Error = "Cannot unregister the local backend" };
        }

        var success = _registry.Unregister(id);
        return new UnregisterBackendResult
        {
            Success = success,
            Error = success ? null : $"Backend '{id}' not found"
        };
    }

    /// <summary>
    /// 设置默认后端
    /// </summary>
    [McpServerTool(Name = "set_default_backend"), Description("Set which backend to use when backendId is omitted in tool calls. The setting persists for the session. Useful when working primarily with a remote backend.")]
    public SetDefaultBackendResult SetDefaultBackend(
        [Description("Backend ID to set as default (must be a registered backend)")] string id)
    {
        var success = _registry.SetDefault(id);
        return new SetDefaultBackendResult
        {
            Success = success,
            Error = success ? null : $"Backend '{id}' not found"
        };
    }

    /// <summary>
    /// 检查后端健康状态
    /// </summary>
    [McpServerTool(Name = "check_backend_health"), Description("Verify backend connectivity and responsiveness. Returns true if the backend responds to health check. Use to diagnose connection issues or monitor remote backends.")]
    public async Task<CheckHealthResult> CheckBackendHealth(
        [Description("Backend ID to check. Omit to check the default backend.")] string? id = null)
    {
        var backend = _registry.Get(id);
        if (backend == null)
        {
            return new CheckHealthResult { Success = false, Error = "Backend not found. Use 'list_backends' to check registered backends, or ensure the local backend is enabled." };
        }

        var healthy = await backend.CheckHealthAsync();
        return new CheckHealthResult
        {
            Success = true,
            BackendId = backend.Id,
            IsHealthy = healthy,
            LastCheck = backend.LastHealthCheck?.ToString("O")
        };
    }
}

public record ListBackendsResult
{
    public bool Success { get; init; }
    public required BackendInfoDto[] Backends { get; init; }
    public string? Error { get; init; }
}

public record BackendInfoDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public bool IsHealthy { get; init; }
    public bool IsDefault { get; init; }
    public string? LastHealthCheck { get; init; }
}

public record RegisterBackendResult
{
    public bool Success { get; init; }
    public string? BackendId { get; init; }
    public string? Message { get; init; }
    public string? Error { get; init; }
}

public record UnregisterBackendResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
}

public record SetDefaultBackendResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
}

public record CheckHealthResult
{
    public bool Success { get; init; }
    public string? BackendId { get; init; }
    public bool IsHealthy { get; init; }
    public string? LastCheck { get; init; }
    public string? Error { get; init; }
}
