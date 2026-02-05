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

## Environment Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `API_KEYS` | Comma-separated list of valid API keys | `key1,key2,key3` |
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | `Development`, `Production` |
| `DOTNET_ENVIRONMENT` | .NET environment | `Development`, `Production` |

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

## API Key Authentication

The Backend service supports API Key authentication for securing HTTP endpoints.

### Setup

```bash
# Set API keys (comma-separated for multiple keys)
export API_KEYS="your-secret-key-1,your-secret-key-2"
```

### Supported Headers

| Header | Format | Example |
|--------|--------|---------|
| `X-API-Key` | Direct key | `X-API-Key: your-api-key` |
| `Authorization` | Bearer token | `Authorization: Bearer your-api-key` |

### Excluded Paths

The following paths are excluded from authentication:
- `/` - Root endpoint
- `/health` - Health check endpoint
- `/openapi` - OpenAPI specification

### Security Notes

- **Development**: Authentication is disabled if no API keys are configured (warning logged)
- **Production**: A critical warning is logged if running without API keys configured
- Always configure API keys in production environments

## Remote Backend Configuration

DotNet MCP supports connecting to remote backends for distributed analysis:

```
# Use in Claude
Register remote backend http://remote-server:5000 named "remote-1"
```

### Configuration via appsettings.json

```json
{
  "McpServer": {
    "EnableLocalBackend": true,
    "ServerName": "dotnet-mcp",
    "ServerVersion": "1.0.0",
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

### RemoteBackend Configuration Options

| Property | Required | Description | Default |
|----------|----------|-------------|---------|
| `Id` | Yes | Unique backend identifier | - |
| `Name` | Yes | Display name for the backend | - |
| `Endpoint` | Yes | HTTP URL of the remote backend | - |
| `ApiKey` | No | API Key for authentication | `null` |
| `TimeoutSeconds` | No | Request timeout in seconds | `30` |

### Deployment Scenarios

#### 1. Single Local Backend (Default)

```json
{
  "McpServer": {
    "EnableLocalBackend": true
  }
}
```

#### 2. Local + Remote Backends

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

#### 3. Gateway Mode (Remote Only)

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

### Backend Management Commands

- `list_backends` - List all backends
- `register_remote_backend` - Register remote backend
- `unregister_backend` - Unregister backend
- `set_default_backend` - Set default backend
- `check_backend_health` - Check backend health status

## Next Steps

- [Tools Reference](tools-reference.md) - View all MCP tools details
