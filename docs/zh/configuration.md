# 配置说明

[English](../en/configuration.md) | 中文

## 命令行参数

| 参数 | 说明 | 默认值 |
|------|------|--------|
| `--stdio` | 启用 stdio 模式（用于 Claude Desktop） | 否 |
| `--port` | HTTP 模式端口 | 5000 |

### 示例

```bash
# Stdio 模式
dotnet run --project src/DotNetMcp.Server -- --stdio

# 指定端口的 HTTP 模式
dotnet run --project src/DotNetMcp.Server -- --port 8080
```

## 环境变量

| 变量 | 说明 | 示例 |
|------|------|------|
| `API_KEYS` | 逗号分隔的有效 API Key 列表 | `key1,key2,key3` |
| `ASPNETCORE_ENVIRONMENT` | 运行时环境 | `Development`, `Production` |
| `DOTNET_ENVIRONMENT` | .NET 环境 | `Development`, `Production` |

## 配置文件

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "McpServer": {
    "Name": "DotNet MCP",
    "Version": "0.0.3"
  }
}
```

## Claude Desktop 配置

### 基本配置

```json
{
  "mcpServers": {
    "dotnet-mcp": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/path/to/DotNetMCP/src/DotNetMcp.Server",
        "--",
        "--stdio"
      ]
    }
  }
}
```

### 使用已编译的可执行文件

```json
{
  "mcpServers": {
    "dotnet-mcp": {
      "command": "/path/to/DotNetMcp.Server",
      "args": ["--stdio"]
    }
  }
}
```

### 设置环境变量

```json
{
  "mcpServers": {
    "dotnet-mcp": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/src/DotNetMcp.Server", "--", "--stdio"],
      "env": {
        "DOTNET_ENVIRONMENT": "Production"
      }
    }
  }
}
```

## API Key 认证

Backend 服务支持 API Key 认证以保护 HTTP 端点。

### 配置

```bash
# 设置 API keys（多个 key 用逗号分隔）
export API_KEYS="your-secret-key-1,your-secret-key-2"
```

### 支持的请求头

| 请求头 | 格式 | 示例 |
|--------|------|------|
| `X-API-Key` | 直接传递 key | `X-API-Key: your-api-key` |
| `Authorization` | Bearer 令牌 | `Authorization: Bearer your-api-key` |

### 排除路径

以下路径无需认证：
- `/` - 根端点
- `/health` - 健康检查端点
- `/openapi` - OpenAPI 规范

### 安全说明

- **开发环境**：如果未配置 API keys，认证将被禁用（记录警告）
- **生产环境**：如果未配置 API keys，将记录严重警告
- 生产环境中请务必配置 API keys

## 远程后端配置

DotNet MCP 支持连接远程后端，实现分布式分析：

```
# 在 Claude 中使用
注册远程后端 http://remote-server:5000 命名为 "remote-1"
```

### 通过 appsettings.json 配置

```json
{
  "McpServer": {
    "EnableLocalBackend": true,
    "ServerName": "dotnet-mcp",
    "ServerVersion": "0.0.3",
    "HealthCheckIntervalSeconds": 30,
    "RemoteBackends": [
      {
        "Id": "remote-1",
        "Name": "Analysis Server",
        "Endpoint": "http://server:5000",
        "ApiKey": "your-api-key",
        "TimeoutSeconds": 30
      },
      {
        "Id": "remote-2",
        "Name": "Build Server",
        "Endpoint": "http://build-server:5000",
        "ApiKey": "another-api-key",
        "TimeoutSeconds": 60
      }
    ]
  }
}
```

### RemoteBackend 配置选项

| 属性 | 必需 | 说明 | 默认值 |
|------|------|------|--------|
| `Id` | 是 | 唯一后端标识符 | - |
| `Name` | 是 | 后端显示名称 | - |
| `Endpoint` | 是 | 远程后端的 HTTP URL | - |
| `ApiKey` | 否 | 认证用 API Key | `null` |
| `TimeoutSeconds` | 否 | 请求超时时间（秒） | `30` |

### 部署场景

#### 1. 单本地后端（默认）

```json
{
  "McpServer": {
    "EnableLocalBackend": true
  }
}
```

#### 2. 本地 + 远程后端

```json
{
  "McpServer": {
    "EnableLocalBackend": true,
    "RemoteBackends": [
      {
        "Id": "remote-analysis",
        "Name": "Remote Analysis Server",
        "Endpoint": "http://analysis-server:5000",
        "ApiKey": "secret-key"
      }
    ]
  }
}
```

#### 3. 网关模式（仅远程）

```json
{
  "McpServer": {
    "EnableLocalBackend": false,
    "RemoteBackends": [
      {
        "Id": "backend-1",
        "Name": "Backend 1",
        "Endpoint": "http://backend1:5000",
        "ApiKey": "key1"
      },
      {
        "Id": "backend-2",
        "Name": "Backend 2",
        "Endpoint": "http://backend2:5000",
        "ApiKey": "key2"
      }
    ]
  }
}
```

### 后端管理命令

- `list_backends` - 列出所有后端
- `register_remote_backend` - 注册远程后端
- `unregister_backend` - 注销后端
- `set_default_backend` - 设置默认后端
- `check_backend_health` - 检查后端健康状态

## 下一步

- [工具参考](tools-reference.md) - 查看所有 MCP 工具详情
