using DotNetMcp.Backend.Core.Context;
using System.Collections.Concurrent;

namespace DotNetMcp.Backend.Services;

/// <summary>
/// 实例注册表接口 - 统一管理所有程序集实例
/// </summary>
public interface IInstanceRegistry
{
    /// <summary>
    /// 注册程序集上下文
    /// </summary>
    void Register(string mvid, AssemblyContext context);

    /// <summary>
    /// 注销程序集上下文
    /// </summary>
    bool Unregister(string mvid);

    /// <summary>
    /// 获取程序集上下文，如果 mvid 为空则返回默认实例
    /// </summary>
    AssemblyContext? Get(string? mvid = null);

    /// <summary>
    /// 尝试获取程序集上下文
    /// </summary>
    bool TryGet(string mvid, out AssemblyContext? context);

    /// <summary>
    /// 获取所有已注册的上下文
    /// </summary>
    IReadOnlyList<AssemblyContext> GetAll();

    /// <summary>
    /// 获取所有已注册的上下文及其 MVID
    /// </summary>
    IReadOnlyDictionary<string, AssemblyContext> GetAllWithKeys();

    /// <summary>
    /// 检查是否存在指定的实例
    /// </summary>
    bool Contains(string mvid);

    /// <summary>
    /// 已注册实例数量
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 默认实例 MVID
    /// </summary>
    string? DefaultInstance { get; set; }
}

/// <summary>
/// 实例注册表实现 - 线程安全的单例服务
/// </summary>
public class InstanceRegistry : IInstanceRegistry
{
    private readonly ConcurrentDictionary<string, AssemblyContext> _contexts = new();
    private string? _defaultInstance;
    private readonly object _defaultLock = new();

    public string? DefaultInstance
    {
        get => _defaultInstance;
        set
        {
            lock (_defaultLock)
            {
                if (value == null || _contexts.ContainsKey(value))
                {
                    _defaultInstance = value;
                }
            }
        }
    }

    public int Count => _contexts.Count;

    public void Register(string mvid, AssemblyContext context)
    {
        _contexts[mvid] = context;

        // 如果没有默认实例，设置第一个为默认
        lock (_defaultLock)
        {
            _defaultInstance ??= mvid;
        }
    }

    public bool Unregister(string mvid)
    {
        var removed = _contexts.TryRemove(mvid, out _);

        if (removed)
        {
            lock (_defaultLock)
            {
                // 如果删除的是默认实例，重新设置默认
                if (_defaultInstance == mvid)
                {
                    _defaultInstance = _contexts.Keys.FirstOrDefault();
                }
            }
        }

        return removed;
    }

    public AssemblyContext? Get(string? mvid = null)
    {
        if (!string.IsNullOrEmpty(mvid))
        {
            return _contexts.TryGetValue(mvid, out var ctx) ? ctx : null;
        }

        // 返回默认实例
        var defaultMvid = _defaultInstance;
        if (defaultMvid != null && _contexts.TryGetValue(defaultMvid, out var defaultCtx))
        {
            return defaultCtx;
        }

        // 如果没有默认实例，返回第一个
        return _contexts.Values.FirstOrDefault();
    }

    public bool TryGet(string mvid, out AssemblyContext? context)
    {
        var result = _contexts.TryGetValue(mvid, out var ctx);
        context = ctx;
        return result;
    }

    public IReadOnlyList<AssemblyContext> GetAll()
    {
        return _contexts.Values.ToList();
    }

    public IReadOnlyDictionary<string, AssemblyContext> GetAllWithKeys()
    {
        return new Dictionary<string, AssemblyContext>(_contexts);
    }

    public bool Contains(string mvid)
    {
        return _contexts.ContainsKey(mvid);
    }
}
