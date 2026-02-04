namespace DotNetMcp.Server.Backend;

/// <summary>
/// 后端注册表接口
/// </summary>
public interface IBackendRegistry
{
    /// <summary>
    /// 注册后端
    /// </summary>
    void Register(IBackend backend);

    /// <summary>
    /// 注销后端
    /// </summary>
    bool Unregister(string backendId);

    /// <summary>
    /// 获取后端
    /// </summary>
    IBackend? Get(string? backendId = null);

    /// <summary>
    /// 获取所有后端
    /// </summary>
    IReadOnlyList<IBackend> GetAll();

    /// <summary>
    /// 设置默认后端
    /// </summary>
    bool SetDefault(string backendId);

    /// <summary>
    /// 默认后端 ID
    /// </summary>
    string? DefaultBackendId { get; }

    /// <summary>
    /// 后端数量
    /// </summary>
    int Count { get; }
}
