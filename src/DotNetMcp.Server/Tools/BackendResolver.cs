using DotNetMcp.Server.Backend;

namespace DotNetMcp.Server.Tools;

/// <summary>
/// 用于统一 backend null-check 的辅助扩展
/// </summary>
public static class BackendResolver
{
    public const string NoBackendErrorMessage =
        "No backend available. Use 'list_backends' to check registered backends, or ensure the local backend is enabled.";

    /// <summary>
    /// 当按指定 ID 查找 backend 不存在时使用的错误消息（明确 backend ID 场景）。
    /// </summary>
    public static string BackendNotFoundMessage(string? id) =>
        id == null
            ? NoBackendErrorMessage
            : $"Backend '{id}' not found. Use 'list_backends' to see registered backends.";

    /// <summary>
    /// 解析 backend，若不存在则填充 error 输出参数。
    /// </summary>
    public static IBackend? TryGet(this IBackendRegistry registry, string? backendId, out string? error)
    {
        var backend = registry.Get(backendId);
        error = backend == null ? NoBackendErrorMessage : null;
        return backend;
    }
}
