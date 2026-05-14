using DotNetMcp.Backend.Core.Context;

namespace DotNetMcp.Backend.Services;

/// <summary>
/// 程序集管理器接口 - 统一管理所有程序集实例的生命周期
/// </summary>
public interface IAssemblyManager
{
    /// <summary>
    /// 异步加载程序集
    /// </summary>
    /// <param name="path">程序集路径</param>
    /// <param name="searchPaths">依赖搜索路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>加载结果</returns>
    Task<AssemblyLoadResult> LoadAsync(string path, IEnumerable<string>? searchPaths = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 卸载程序集
    /// </summary>
    /// <param name="mvid">程序集 MVID</param>
    /// <returns>是否成功卸载</returns>
    bool Unload(string mvid);

    /// <summary>
    /// 获取程序集上下文
    /// </summary>
    /// <param name="mvid">程序集 MVID，为 null 时返回默认实例</param>
    /// <returns>程序集上下文，不存在时返回 null</returns>
    AssemblyContext? Get(string? mvid = null);

    /// <summary>
    /// 获取所有已加载的程序集
    /// </summary>
    IReadOnlyList<AssemblyContext> GetAll();

    /// <summary>
    /// 检查程序集是否存在
    /// </summary>
    /// <param name="mvid">程序集 MVID</param>
    bool Exists(string mvid);

    /// <summary>
    /// 已加载的程序集数量
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 默认实例的 MVID
    /// </summary>
    string? DefaultMvid { get; }

    /// <summary>
    /// 设置默认实例
    /// </summary>
    /// <param name="mvid">程序集 MVID</param>
    /// <returns>是否设置成功</returns>
    bool SetDefault(string mvid);

    // ---------- Alias 支持 ----------

    /// <summary>
    /// 注册 alias → mvid 映射
    /// </summary>
    /// <param name="alias">别名（长度 1-32，[A-Za-z0-9_-]，不能是纯数字或保留字）</param>
    /// <param name="mvid">目标 MVID</param>
    /// <param name="overwrite">是否允许覆盖已有 alias</param>
    bool RegisterAlias(string alias, string mvid, bool overwrite = false);

    /// <summary>
    /// 取消注册 alias
    /// </summary>
    bool UnregisterAlias(string alias);

    /// <summary>
    /// 解析 aliasOrMvid：先查 alias 表，找不到直接当 mvid 返回
    /// </summary>
    string? ResolveAlias(string? aliasOrMvid);

    /// <summary>
    /// 获取全部 alias 映射
    /// </summary>
    IReadOnlyDictionary<string, string> GetAliases();

    /// <summary>
    /// 从持久化存储恢复 alias 并重新加载对应程序集（按需触发，不自动运行）
    /// </summary>
    Task<int> RestorePersistedAssembliesAsync(CancellationToken ct = default);
}
