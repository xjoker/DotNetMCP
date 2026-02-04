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
    "Version": "0.0.1"
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

## 远程后端配置

DotNet MCP 支持连接远程后端，实现分布式分析：

```
# 在 Claude 中使用
注册远程后端 http://remote-server:5000 命名为 "remote-1"
```

### 后端管理命令

- `list_backends` - 列出所有后端
- `register_remote_backend` - 注册远程后端
- `unregister_backend` - 注销后端
- `set_default_backend` - 设置默认后端
- `check_backend_health` - 检查后端健康状态

## 下一步

- [工具参考](tools-reference.md) - 查看所有 MCP 工具详情
