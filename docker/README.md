# DotNetMCP Docker 部署指南

提供两种部署模式：**All-in-One** 和 **拆分模式**。

## 镜像说明

| 镜像 | 说明 | 大小 |
|------|------|------|
| `dotnet-mcp-allinone` | 单容器包含 Backend + MCP Server | ~500MB |
| `dotnet-mcp-backend` | 仅 .NET Backend | ~300MB |
| `dotnet-mcp-server` | 仅 Python MCP Server | ~150MB |

## 端口

| 端口 | 服务 | 说明 |
|------|------|------|
| 8650 | Backend API | .NET 程序集分析 API |
| 8651 | MCP Server | LLM 连接入口 (MCP 协议) |

---

## All-in-One 模式

单容器运行所有服务，适合快速部署和开发测试。

### 构建

```bash
docker build -t dotnet-mcp-allinone -f docker/Dockerfile.allinone .
```

### 运行

```bash
docker run -d \
  --name dotnet-mcp \
  -p 8650:8650 \
  -p 8651:8651 \
  -v ./assemblies:/data/assemblies \
  dotnet-mcp-allinone
```

### 使用 Docker Compose

```bash
docker compose -f docker/docker-compose.allinone.yml up -d
```

---

## 拆分模式

Backend 和 MCP Server 分别运行在独立容器，适合生产环境和扩展部署。

### 构建

```bash
# 构建 Backend
docker build -t dotnet-mcp-backend -f docker/Dockerfile.backend backend-service/

# 构建 MCP Server
docker build -t dotnet-mcp-server -f docker/Dockerfile.mcp-server .
```

### 使用 Docker Compose

```bash
docker compose -f docker/docker-compose.yml up -d
```

---

## 环境变量

### Backend 服务

| 变量 | 默认值 | 说明 |
|------|--------|------|
| `ASPNETCORE_URLS` | `http://+:8650` | 监听地址 |
| `ASPNETCORE_ENVIRONMENT` | `Production` | 运行环境 |
| `API_KEYS` | (空) | API Key 认证，逗号分隔 |
| `TZ` | `UTC` | 时区 |

### MCP Server

| 变量 | 默认值 | 说明 |
|------|--------|------|
| `MCP_HOST` | `0.0.0.0` | 监听地址 |
| `MCP_PORT` | `8651` | 监听端口 |
| `BACKEND_HOST` | `backend` / `127.0.0.1` | Backend 地址 |
| `BACKEND_PORT` | `8650` | Backend 端口 |

---

## 数据卷

| 路径 | 说明 |
|------|------|
| `/data/assemblies` | 上传的程序集存储 |
| `/app/cache` | 分析缓存 |

---

## 健康检查

```bash
# Backend
curl http://localhost:8650/health

# MCP Server
curl http://localhost:8651/health
```

---

## 使用示例

### 1. 启动服务

```bash
docker compose -f docker/docker-compose.allinone.yml up -d
```

### 2. 上传并分析程序集

```bash
# 加载程序集
curl -X POST http://localhost:8650/assembly/load \
  -H "Content-Type: application/json" \
  -d '{"path": "/data/assemblies/sample.dll"}'
```

### 3. 连接 LLM (Claude)

在 Claude Desktop 配置中添加 MCP Server：

```json
{
  "mcpServers": {
    "dotnet-mcp": {
      "url": "http://localhost:8651"
    }
  }
}
```

---

## 故障排查

```bash
# 查看日志
docker logs dotnet-mcp-allinone

# 查看服务状态 (All-in-One)
docker exec dotnet-mcp-allinone supervisorctl status

# 进入容器
docker exec -it dotnet-mcp-allinone bash
```
