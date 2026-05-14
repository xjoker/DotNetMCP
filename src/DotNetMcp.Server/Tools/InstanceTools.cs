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
        var backend = _registry.TryGet(id, out _);
        if (backend == null)
        {
            return new CheckHealthResult { Success = false, Error = BackendResolver.BackendNotFoundMessage(id) };
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

    /// <summary>
    /// 注册程序集 alias
    /// </summary>
    [McpServerTool(Name = "register_assembly_alias"), Description("Register a short alias for a loaded assembly MVID. After registration, all tools that accept 'mvid' can use the alias instead of the full GUID. Rules: 1-32 chars, [A-Za-z0-9_-], not all-digits, not reserved words (default/local/null).")]
    public async Task<AliasToolResult> RegisterAssemblyAlias(
        [Description("Short alias to register (e.g. 'main', 'v1', 'target'). 1-32 chars, [A-Za-z0-9_-].")] string alias,
        [Description("Assembly MVID to bind the alias to. Omit to use the current default assembly.")] string? mvid = null,
        [Description("If true, overwrite an existing alias with the same name.")] bool overwrite = false,
        [Description("Backend ID. Omit to use the default backend.")] string? backendId = null)
    {
        var backend = _registry.TryGet(backendId, out var err1);
        if (backend == null)
            return new AliasToolResult { Success = false, Error = err1 };

        // 如果 mvid 未提供，使用默认
        if (string.IsNullOrEmpty(mvid))
        {
            var assemblies = await backend.ListAssembliesAsync();
            var def = assemblies.FirstOrDefault(a => a.IsDefault) ?? assemblies.FirstOrDefault();
            if (def == null)
                return new AliasToolResult { Success = false, Error = "No assemblies loaded. Load an assembly first." };
            mvid = def.Mvid;
        }

        var result = await backend.RegisterAssemblyAliasAsync(alias, mvid, overwrite);
        return new AliasToolResult
        {
            Success = result.IsSuccess,
            Alias = result.Alias,
            Mvid = result.Mvid,
            Error = result.ErrorMessage
        };
    }

    /// <summary>
    /// 取消注册程序集 alias
    /// </summary>
    [McpServerTool(Name = "unregister_assembly_alias"), Description("Remove a previously registered assembly alias. The underlying assembly remains loaded; only the alias mapping is deleted.")]
    public async Task<AliasToolResult> UnregisterAssemblyAlias(
        [Description("Alias to remove.")] string alias,
        [Description("Backend ID. Omit to use the default backend.")] string? backendId = null)
    {
        var backend = _registry.TryGet(backendId, out var err2);
        if (backend == null)
            return new AliasToolResult { Success = false, Error = err2 };

        var result = await backend.UnregisterAssemblyAliasAsync(alias);
        return new AliasToolResult
        {
            Success = result.IsSuccess,
            Alias = result.Alias,
            Error = result.ErrorMessage
        };
    }

    /// <summary>
    /// 列出所有程序集 alias
    /// </summary>
    [McpServerTool(Name = "list_assembly_aliases"), Description("List all registered assembly aliases for the specified backend. Each alias maps a short name to a full MVID.")]
    public async Task<ListAliasesToolResult> ListAssemblyAliases(
        [Description("Backend ID. Omit to use the default backend.")] string? backendId = null)
    {
        var backend = _registry.TryGet(backendId, out var err3);
        if (backend == null)
            return new ListAliasesToolResult { Success = false, Error = err3 };

        var result = await backend.ListAssemblyAliasesAsync();
        return new ListAliasesToolResult
        {
            Success = result.IsSuccess,
            Aliases = result.Aliases.Select(a => new AliasEntryDto { Alias = a.Alias, Mvid = a.Mvid }).ToArray(),
            Error = result.ErrorMessage
        };
    }

    /// <summary>
    /// 恢复持久化程序集
    /// </summary>
    [McpServerTool(Name = "instance_restore_persisted"), Description("Reload assemblies from persisted alias entries saved to disk from a previous session. Returns the count of successfully restored assemblies. Failed entries are removed from persistence.")]
    public async Task<RestorePersistedToolResult> RestorePersistedAssemblies(
        [Description("Backend ID. Omit to use the default backend.")] string? backendId = null)
    {
        var backend = _registry.TryGet(backendId, out var err4);
        if (backend == null)
            return new RestorePersistedToolResult { Success = false, Error = err4 };

        var result = await backend.RestorePersistedAssembliesAsync();
        return new RestorePersistedToolResult
        {
            Success = result.IsSuccess,
            RestoredCount = result.RestoredCount,
            Error = result.ErrorMessage
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

public record AliasToolResult
{
    public bool Success { get; init; }
    public string? Alias { get; init; }
    public string? Mvid { get; init; }
    public string? Error { get; init; }
}

public record ListAliasesToolResult
{
    public bool Success { get; init; }
    public AliasEntryDto[]? Aliases { get; init; }
    public string? Error { get; init; }
}

public record AliasEntryDto
{
    public required string Alias { get; init; }
    public required string Mvid { get; init; }
}

public record RestorePersistedToolResult
{
    public bool Success { get; init; }
    public int RestoredCount { get; init; }
    public string? Error { get; init; }
}
