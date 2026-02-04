namespace DotNetMcp.Server.Configuration;

/// <summary>
/// MCP Server 配置选项
/// </summary>
public class McpServerOptions
{
    public const string SectionName = "McpServer";

    /// <summary>
    /// 是否启用本地后端
    /// </summary>
    public bool EnableLocalBackend { get; set; } = true;

    /// <summary>
    /// 服务器名称
    /// </summary>
    public string ServerName { get; set; } = "dotnet-mcp";

    /// <summary>
    /// 服务器版本
    /// </summary>
    public string ServerVersion { get; set; } = "1.0.0";

    /// <summary>
    /// 远程后端配置列表
    /// </summary>
    public List<RemoteBackendConfig> RemoteBackends { get; set; } = new();

    /// <summary>
    /// 健康检查间隔（秒）
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 30;
}

/// <summary>
/// 远程后端配置
/// </summary>
public class RemoteBackendConfig
{
    /// <summary>
    /// 后端唯一标识
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// 后端名称
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// 后端端点 URL
    /// </summary>
    public required string Endpoint { get; set; }

    /// <summary>
    /// API 密钥（可选）
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// 连接超时（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}
