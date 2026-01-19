using Mono.Cecil;

namespace DotNetMcp.Backend.Core.Context;

/// <summary>
/// 自定义程序集解析器 - 实现三级依赖解析策略
/// </summary>
public class CustomAssemblyResolver : DefaultAssemblyResolver
{
    private readonly List<string> _searchPaths;
    private readonly Dictionary<string, AssemblyDefinition?> _cache;
    private readonly object _lock = new();

    /// <summary>
    /// 解析统计
    /// </summary>
    public ResolverStatistics Statistics { get; } = new();

    public CustomAssemblyResolver(IEnumerable<string>? additionalSearchPaths = null)
    {
        _searchPaths = new List<string>();
        _cache = new Dictionary<string, AssemblyDefinition?>(StringComparer.OrdinalIgnoreCase);

        // 添加标准搜索路径
        AddStandardSearchPaths();

        // 添加用户指定的搜索路径
        if (additionalSearchPaths != null)
        {
            foreach (var path in additionalSearchPaths)
            {
                if (Directory.Exists(path))
                {
                    AddSearchDirectory(path);
                    _searchPaths.Add(path);
                }
            }
        }
    }

    /// <summary>
    /// 添加标准搜索路径
    /// </summary>
    private void AddStandardSearchPaths()
    {
        // .NET Framework GAC
        var gacPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "Microsoft.NET", "assembly"
        );
        if (Directory.Exists(gacPath))
        {
            AddSearchDirectory(gacPath);
        }

        // .NET Core/5+ 共享框架
        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT")
            ?? (OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet")
                : "/usr/share/dotnet");

        var sharedPath = Path.Combine(dotnetRoot, "shared");
        if (Directory.Exists(sharedPath))
        {
            AddSearchDirectory(sharedPath);
        }

        // NuGet 全局包缓存
        var nugetGlobalPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget", "packages"
        );
        if (Directory.Exists(nugetGlobalPath))
        {
            _searchPaths.Add(nugetGlobalPath);
        }
    }

    /// <summary>
    /// 三级依赖解析策略
    /// </summary>
    public override AssemblyDefinition? Resolve(AssemblyNameReference name)
    {
        lock (_lock)
        {
            Statistics.TotalAttempts++;

            // 检查缓存
            var cacheKey = name.FullName;
            if (_cache.TryGetValue(cacheKey, out var cached))
            {
                Statistics.CacheHits++;
                return cached;
            }

            AssemblyDefinition? result = null;

            try
            {
                // Level 1: 使用 Cecil 默认解析器
                result = base.Resolve(name);
                if (result != null)
                {
                    Statistics.Level1Success++;
                    _cache[cacheKey] = result;
                    return result;
                }
            }
            catch
            {
                // 继续尝试其他策略
            }

            try
            {
                // Level 2: 在自定义搜索路径中查找
                result = SearchInPaths(name);
                if (result != null)
                {
                    Statistics.Level2Success++;
                    _cache[cacheKey] = result;
                    return result;
                }
            }
            catch
            {
                // 继续尝试其他策略
            }

            // Level 3: 标记为外部引用（无法解析但允许继续）
            Statistics.Level3Fallback++;
            _cache[cacheKey] = null; // 缓存失败结果，避免重复尝试
            
            // 不抛出异常，返回 null 允许继续分析
            return null;
        }
    }

    /// <summary>
    /// 在搜索路径中查找程序集
    /// </summary>
    private AssemblyDefinition? SearchInPaths(AssemblyNameReference name)
    {
        var fileName = $"{name.Name}.dll";
        
        foreach (var searchPath in _searchPaths)
        {
            // 直接在路径中查找
            var directPath = Path.Combine(searchPath, fileName);
            if (File.Exists(directPath))
            {
                return AssemblyDefinition.ReadAssembly(directPath);
            }

            // 递归搜索（限制深度）
            var found = FindInDirectory(searchPath, fileName, maxDepth: 3);
            if (found != null)
            {
                return AssemblyDefinition.ReadAssembly(found);
            }
        }

        return null;
    }

    /// <summary>
    /// 在目录中递归查找文件
    /// </summary>
    private static string? FindInDirectory(string directory, string fileName, int maxDepth, int currentDepth = 0)
    {
        if (currentDepth >= maxDepth)
            return null;

        try
        {
            // 当前目录
            var filePath = Path.Combine(directory, fileName);
            if (File.Exists(filePath))
                return filePath;

            // 子目录
            foreach (var subDir in Directory.GetDirectories(directory))
            {
                var found = FindInDirectory(subDir, fileName, maxDepth, currentDepth + 1);
                if (found != null)
                    return found;
            }
        }
        catch
        {
            // 忽略访问权限错误
        }

        return null;
    }

    /// <summary>
    /// 获取解析成功率
    /// </summary>
    public double GetSuccessRate()
    {
        return Statistics.TotalAttempts == 0
            ? 0
            : (double)(Statistics.Level1Success + Statistics.Level2Success) / Statistics.TotalAttempts * 100;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // 清理缓存中的程序集
            foreach (var assembly in _cache.Values.Where(a => a != null))
            {
                assembly?.Dispose();
            }
            _cache.Clear();
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// 解析器统计信息
/// </summary>
public class ResolverStatistics
{
    public int TotalAttempts { get; set; }
    public int CacheHits { get; set; }
    public int Level1Success { get; set; }  // Cecil 默认解析成功
    public int Level2Success { get; set; }  // 自定义路径解析成功
    public int Level3Fallback { get; set; } // 降级为外部引用

    public double SuccessRate => TotalAttempts == 0
        ? 0
        : (double)(Level1Success + Level2Success) / TotalAttempts * 100;
}
