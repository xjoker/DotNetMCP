using Microsoft.CodeAnalysis;
using System.Reflection;

namespace DotNetMcp.Backend.Core.Compilation;

/// <summary>
/// 引用程序集提供器 - 管理编译所需的引用程序集
/// </summary>
public class ReferenceAssemblyProvider
{
    private readonly Dictionary<string, List<MetadataReference>> _cache = new();
    private readonly object _lock = new();

    /// <summary>
    /// 获取指定框架的引用程序集
    /// </summary>
    /// <param name="targetFramework">目标框架（如 "net9.0"），null 表示当前运行时</param>
    /// <returns>元数据引用列表</returns>
    public IEnumerable<MetadataReference> GetReferences(string? targetFramework = null)
    {
        var key = targetFramework ?? "current";

        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var references = BuildReferences(targetFramework);
            _cache[key] = references;
            return references;
        }
    }

    /// <summary>
    /// 构建引用程序集列表
    /// </summary>
    private List<MetadataReference> BuildReferences(string? targetFramework)
    {
        var references = new List<MetadataReference>();

        // 核心程序集（始终需要）
        AddCoreReferences(references);

        // 根据目标框架添加特定引用
        if (targetFramework != null)
        {
            AddFrameworkSpecificReferences(references, targetFramework);
        }

        return references;
    }

    /// <summary>
    /// 添加核心程序集引用
    /// </summary>
    private void AddCoreReferences(List<MetadataReference> references)
    {
        // System.Runtime
        var systemRuntime = Assembly.Load("System.Runtime");
        references.Add(MetadataReference.CreateFromFile(systemRuntime.Location));

        // System.Private.CoreLib
        references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));

        // System.Console
        var consoleAssembly = Assembly.Load("System.Console");
        references.Add(MetadataReference.CreateFromFile(consoleAssembly.Location));

        // System.Linq
        var linqAssembly = Assembly.Load("System.Linq");
        references.Add(MetadataReference.CreateFromFile(linqAssembly.Location));

        // System.Collections
        var collectionsAssembly = Assembly.Load("System.Collections");
        references.Add(MetadataReference.CreateFromFile(collectionsAssembly.Location));
    }

    /// <summary>
    /// 添加框架特定引用
    /// </summary>
    private void AddFrameworkSpecificReferences(List<MetadataReference> references, string targetFramework)
    {
        // 可以根据 targetFramework 添加特定版本的引用
        // 当前简化实现，使用当前运行时的引用
    }

    /// <summary>
    /// 添加自定义引用程序集
    /// </summary>
    /// <param name="assemblyPath">程序集路径</param>
    public void AddCustomReference(string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException($"Assembly not found: {assemblyPath}");
        }

        lock (_lock)
        {
            // 清除缓存，强制重新构建
            _cache.Clear();
        }
    }

    /// <summary>
    /// 清除缓存
    /// </summary>
    public void ClearCache()
    {
        lock (_lock)
        {
            _cache.Clear();
        }
    }

    /// <summary>
    /// 获取缓存的框架数量
    /// </summary>
    public int GetCachedFrameworkCount()
    {
        lock (_lock)
        {
            return _cache.Count;
        }
    }
}
