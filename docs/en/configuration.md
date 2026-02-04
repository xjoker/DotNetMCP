# Configuration

English | [中文](../zh/configuration.md)

## Command Line Arguments

| Argument | Description | Default |
|----------|-------------|---------|
| `--stdio` | Enable stdio mode (for Claude Desktop) | No |
| `--port` | HTTP mode port | 5000 |

### Examples

```bash
# Stdio mode
dotnet run --project src/DotNetMcp.Server -- --stdio

# HTTP mode with custom port
dotnet run --project src/DotNetMcp.Server -- --port 8080
```

## Configuration File

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

## Claude Desktop Configuration

### Basic Configuration

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

### Using Compiled Executable

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

### Setting Environment Variables

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

## Remote Backend Configuration

DotNet MCP supports connecting to remote backends for distributed analysis:

```
# Use in Claude
Register remote backend http://remote-server:5000 named "remote-1"
```

### Backend Management Commands

- `list_backends` - List all backends
- `register_remote_backend` - Register remote backend
- `unregister_backend` - Unregister backend
- `set_default_backend` - Set default backend
- `check_backend_health` - Check backend health status

## Next Steps

- [Tools Reference](tools-reference.md) - View all MCP tools details
