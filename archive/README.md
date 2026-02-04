# 归档目录

此目录包含已废弃的旧版代码，仅供参考。

## legacy-python/

旧版 Python MCP Server 实现，已被纯 C# 实现 `DotNetMcp.Server` 替代。

### 包含内容

- `mcp-server/` - Python MCP 服务器代码
- `docker/` - 旧版 Docker 配置（Python + C# 双容器方案）
- `tests/` - Python E2E 测试

### 废弃原因

1. 需要同时部署 Python 和 C# 两个进程
2. 跨语言调试困难
3. HTTP 序列化开销

### 新方案

使用 `src/DotNetMcp.Server` 项目，纯 C# 实现，支持：
- 本地模式：零开销直接调用分析引擎
- 远程模式：可连接多个远程后端
- 双传输：stdio（Claude Desktop）和 HTTP（claude mcp add）
