using System.Collections.Concurrent;
using System.Text.RegularExpressions;
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

    // alias 支持
    private readonly ConcurrentDictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly AliasPersistence? _persistence;

    // alias 验证：长度 1-32，[A-Za-z0-9_-]，不能纯数字
    private static readonly Regex _aliasPattern = new(@"^[A-Za-z0-9_-]{1,32}$", RegexOptions.Compiled);
    private static readonly HashSet<string> _reservedAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "default", "local", "null"
    };

    public AssemblyManager(ILogger<AssemblyManager> logger, AliasPersistence? persistence = null)
    {
        _logger = logger;
        _persistence = persistence;

        // 启动时从磁盘恢复 alias 表（仅加载 alias 映射，不自动 LoadAssembly）
        if (_persistence != null)
        {
            var state = _persistence.Load();
            foreach (var (alias, entry) in state.Aliases)
            {
                _aliases.TryAdd(alias, entry.Mvid);
            }
            _logger.LogInformation("Loaded {Count} persisted alias(es) from disk", state.Aliases.Count);
        }
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
                var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                var isAllowed = allowed.Any(p =>
                {
                    var allowedDir = Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    return normalizedPath.StartsWith(allowedDir, comparison)
                        || normalizedPath.Equals(allowedDir.TrimEnd(Path.DirectorySeparatorChar), comparison);
                });
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
            // 先尝试解析 alias，找不到则当 mvid 直接用
            var resolvedMvid = ResolveAlias(mvid) ?? mvid;
            return _contexts.TryGetValue(resolvedMvid, out var context) ? context : null;
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

    // ---------- Alias 实现 ----------

    /// <inheritdoc/>
    public bool RegisterAlias(string alias, string mvid, bool overwrite = false)
    {
        if (!ValidateAlias(alias, out var error))
        {
            _logger.LogWarning("RegisterAlias rejected: {Error}", error);
            return false;
        }

        // 目标 mvid 必须已加载
        if (!_contexts.ContainsKey(mvid))
        {
            _logger.LogWarning("RegisterAlias failed: mvid '{Mvid}' not loaded", mvid);
            return false;
        }

        if (!overwrite && _aliases.ContainsKey(alias))
        {
            _logger.LogWarning("RegisterAlias failed: alias '{Alias}' already exists", alias);
            return false;
        }

        _aliases[alias] = mvid;
        _logger.LogInformation("Registered alias '{Alias}' -> {Mvid}", alias, mvid);

        PersistAliases();
        return true;
    }

    /// <inheritdoc/>
    public bool UnregisterAlias(string alias)
    {
        if (!_aliases.TryRemove(alias, out _))
        {
            _logger.LogWarning("UnregisterAlias: alias '{Alias}' not found", alias);
            return false;
        }

        _logger.LogInformation("Unregistered alias '{Alias}'", alias);
        PersistAliases();
        return true;
    }

    /// <inheritdoc/>
    public string? ResolveAlias(string? aliasOrMvid)
    {
        if (aliasOrMvid == null) return null;
        return _aliases.TryGetValue(aliasOrMvid, out var mvid) ? mvid : aliasOrMvid;
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> GetAliases()
    {
        return _aliases.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public async Task<int> RestorePersistedAssembliesAsync(CancellationToken ct = default)
    {
        if (_persistence == null) return 0;

        var state = _persistence.Load();
        var restored = 0;
        var toRemove = new List<string>();

        foreach (var (alias, entry) in state.Aliases)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var result = await LoadAsync(entry.AssemblyPath, null, ct);
                if (result.IsSuccess && result.Context != null)
                {
                    var newMvid = result.Context.Mvid.ToString();
                    _aliases[alias] = newMvid;
                    restored++;
                    _logger.LogInformation("Restored alias '{Alias}' from {Path}", alias, entry.AssemblyPath);
                }
                else
                {
                    _logger.LogWarning("Failed to restore alias '{Alias}' from {Path}: {Msg}", alias, entry.AssemblyPath, result.ErrorMessage);
                    toRemove.Add(alias);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception restoring alias '{Alias}' from {Path}", alias, entry.AssemblyPath);
                toRemove.Add(alias);
            }
        }

        foreach (var alias in toRemove)
        {
            _aliases.TryRemove(alias, out _);
        }

        if (toRemove.Count > 0)
        {
            PersistAliases();
        }

        return restored;
    }

    private void PersistAliases()
    {
        if (_persistence == null) return;

        try
        {
            var state = new AliasState();
            foreach (var (alias, mvid) in _aliases)
            {
                // 只持久化已加载的程序集所对应的 alias
                if (_contexts.TryGetValue(mvid, out var ctx))
                {
                    state.Aliases[alias] = new AliasEntry
                    {
                        Mvid = mvid,
                        AssemblyPath = ctx.AssemblyPath ?? string.Empty,
                        RegisteredAt = DateTime.UtcNow
                    };
                }
            }
            _persistence.Save(state);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist aliases");
        }
    }

    private static bool ValidateAlias(string alias, out string error)
    {
        if (string.IsNullOrEmpty(alias))
        {
            error = "Alias cannot be empty";
            return false;
        }

        if (!_aliasPattern.IsMatch(alias))
        {
            error = $"Alias '{alias}' contains invalid characters. Only [A-Za-z0-9_-] allowed, max 32 chars";
            return false;
        }

        if (_reservedAliases.Contains(alias))
        {
            error = $"Alias '{alias}' is reserved and cannot be used";
            return false;
        }

        // 不能是纯数字（避免与 MVID 格式混淆）
        if (alias.All(char.IsDigit))
        {
            error = $"Alias '{alias}' cannot be all digits";
            return false;
        }

        error = string.Empty;
        return true;
    }

    // ---------- IDisposable ----------

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
