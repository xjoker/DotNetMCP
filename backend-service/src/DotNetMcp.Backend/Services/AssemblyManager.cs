using System.Collections.Concurrent;
using DotNetMcp.Backend.Core.Context;

namespace DotNetMcp.Backend.Services;

/// <summary>
/// 程序集管理器 - 统一管理所有程序集实例的生命周期
/// </summary>
public class AssemblyManager : IAssemblyManager, IDisposable
{
    private readonly ConcurrentDictionary<string, AssemblyContext> _contexts = new();
    private readonly ILogger<AssemblyManager> _logger;
    private readonly object _defaultLock = new();
    private string? _defaultMvid;
    private bool _disposed;

    public AssemblyManager(ILogger<AssemblyManager> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<AssemblyLoadResult> LoadAsync(string path, IEnumerable<string>? searchPaths = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // 安全验证：规范化路径并检查
            var normalizedPath = Path.GetFullPath(path);

            // 检查文件是否存在
            if (!File.Exists(normalizedPath))
            {
                return AssemblyLoadResult.Failure(
                    AssemblyLoadErrorCode.FileNotFound,
                    $"File not found: {normalizedPath}");
            }

            // 检查文件扩展名
            var extension = Path.GetExtension(normalizedPath).ToLowerInvariant();
            if (extension != ".dll" && extension != ".exe")
            {
                return AssemblyLoadResult.Failure(
                    AssemblyLoadErrorCode.InvalidFormat,
                    "Only .dll and .exe files are allowed");
            }

            // 检查是否在允许的目录内（如果配置了白名单）
            var allowedPaths = Environment.GetEnvironmentVariable("ALLOWED_ASSEMBLY_PATHS");
            if (!string.IsNullOrEmpty(allowedPaths))
            {
                var allowed = allowedPaths.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
                var isAllowed = allowed.Any(p => normalizedPath.StartsWith(Path.GetFullPath(p), StringComparison.OrdinalIgnoreCase));
                if (!isAllowed)
                {
                    _logger.LogWarning("Path {Path} is not in allowed directories", normalizedPath);
                    return AssemblyLoadResult.Failure(
                        AssemblyLoadErrorCode.AccessDenied,
                        "Path is not in allowed directories");
                }
            }

            var context = new AssemblyContext(normalizedPath, searchPaths);
            var result = await context.LoadAsync(cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogError("Failed to load assembly from {Path}: {Message}", path, result.ErrorMessage);
                return result;
            }

            var key = context.Mvid.ToString();

            if (!_contexts.TryAdd(key, context))
            {
                // 已存在相同 MVID 的程序集，释放新加载的
                context.Dispose();
                _logger.LogWarning("Assembly with MVID {Mvid} already loaded, returning existing instance", key);
                return AssemblyLoadResult.Success(_contexts[key]);
            }

            // 如果是第一个加载的程序集，设置为默认
            lock (_defaultLock)
            {
                if (_defaultMvid == null)
                {
                    _defaultMvid = key;
                    _logger.LogInformation("Set default instance to {Mvid}", key);
                }
            }

            _logger.LogInformation("Loaded assembly {Name} (MVID: {Mvid})", context.Name, key);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while loading assembly from {Path}", path);
            return AssemblyLoadResult.Failure(
                AssemblyLoadErrorCode.Unknown,
                $"Failed to load assembly: {ex.Message}",
                ex
            );
        }
    }

    /// <inheritdoc/>
    public bool Unload(string mvid)
    {
        if (!_contexts.TryRemove(mvid, out var context))
        {
            _logger.LogWarning("Attempted to unload non-existent assembly: {Mvid}", mvid);
            return false;
        }

        context.Dispose();

        // 如果卸载的是默认实例，重新选择默认
        lock (_defaultLock)
        {
            if (_defaultMvid == mvid)
            {
                _defaultMvid = _contexts.Keys.FirstOrDefault();
                _logger.LogInformation("Default instance changed to {Mvid}", _defaultMvid ?? "(none)");
            }
        }

        _logger.LogInformation("Unloaded assembly: {Mvid}", mvid);
        return true;
    }

    /// <inheritdoc/>
    public AssemblyContext? Get(string? mvid = null)
    {
        if (mvid != null)
        {
            return _contexts.TryGetValue(mvid, out var context) ? context : null;
        }

        // 返回默认实例
        lock (_defaultLock)
        {
            if (_defaultMvid != null && _contexts.TryGetValue(_defaultMvid, out var defaultContext))
            {
                return defaultContext;
            }
        }

        // 回退：返回第一个
        return _contexts.Values.FirstOrDefault();
    }

    /// <inheritdoc/>
    public IReadOnlyList<AssemblyContext> GetAll()
    {
        return _contexts.Values.ToList().AsReadOnly();
    }

    /// <inheritdoc/>
    public bool Exists(string mvid)
    {
        return _contexts.ContainsKey(mvid);
    }

    /// <inheritdoc/>
    public int Count => _contexts.Count;

    /// <inheritdoc/>
    public string? DefaultMvid
    {
        get
        {
            lock (_defaultLock)
            {
                return _defaultMvid;
            }
        }
    }

    /// <inheritdoc/>
    public bool SetDefault(string mvid)
    {
        if (!_contexts.ContainsKey(mvid))
        {
            _logger.LogWarning("Cannot set default to non-existent assembly: {Mvid}", mvid);
            return false;
        }

        lock (_defaultLock)
        {
            _defaultMvid = mvid;
        }

        _logger.LogInformation("Default instance set to {Mvid}", mvid);
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        foreach (var context in _contexts.Values)
        {
            context.Dispose();
        }
        _contexts.Clear();
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}
