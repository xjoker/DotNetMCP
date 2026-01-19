# DotNet MCP - AI 静态逆向工程 MCP 服务

[![.NET](https://img.shields.io/badge/.NET-10_LTS-512BD4)](https://dotnet.microsoft.com/)
[![Python](https://img.shields.io/badge/Python-3.12+-3776AB)](https://python.org/)
[![MCP](https://img.shields.io/badge/MCP-Protocol-orange)](https://modelcontextprotocol.io/)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

使 AI（Claude/Cursor）能够自主进行 .NET 程序集的静态逆向分析与代码修改。

## ✨ 特性

- 🔍 **元数据分析**：类型、方法、字段的完整结构信息
- 📖 **多格式反编译**：C# / IL / VB 源码输出
- 🔗 **交叉引用**：方法调用、类型使用追踪
- 📊 **调用图构建**：可视化执行流程
- ✏️ **代码修改**：方法体替换、IL 注入、成员操作
- 🧪 **C# 运行时编译**：直接写 C# 代码替换方法
- 👥 **多用户支持**：实例隔离、Token 认证

## 🏗️ 架构

采用分离式架构（参考 [jadx-ai-mcp](https://github.com/xjoker/jadx-ai-mcp)）：

```
┌────────────────────────────────────┐
│     AI 客户端 (Claude/Cursor)       │
└────────────────────────────────────┘
                 │ MCP 协议
                 ▼
┌────────────────────────────────────┐
│   Python MCP Server (FastMCP)      │  ← 端口 8651
│   工具定义 / Prompts / Resources   │
└────────────────────────────────────┘
                 │ HTTP REST API
                 ▼
┌────────────────────────────────────┐
│   C# 后端服务 (ASP.NET Core)       │  ← 端口 8650
│   Mono.Cecil / ILSpy / Roslyn      │
└────────────────────────────────────┘
```

## 📦 目录结构

```
DotNetMCP/
├── mcp-server/              # Python MCP Server
│   ├── src/server/          # 服务核心
│   │   ├── tools/           # MCP 工具（analysis/modification/instance）
│   │   ├── prompts.py       # MCP Prompts
│   │   └── resources.py     # MCP Resources
│   ├── data/config/         # TOML 配置
│   └── pyproject.toml
│
├── backend-service/         # C# 后端服务
│   └── src/DotNetMcp.Backend/   # ASP.NET Core Web API
│
├── docker/                  # Docker 部署
├── docs/                    # 文档
├── DEVELOPMENT.md           # 开发指南
└── README.md
```

## 🚀 快速开始

### 前置条件

- Python >= 3.12 (推荐 3.14)
- .NET SDK 10.0
- Docker（可选，用于部署）

### 1. 启动后端服务

```bash
cd backend-service/src/DotNetMcp.Backend
dotnet run
# 服务运行在 http://localhost:8650
```

### 2. 启动 MCP Server

```bash
cd mcp-server
python -m venv .venv
source .venv/bin/activate  # Windows: .\.venv\Scripts\activate
pip install -r requirements.txt
python dotnetmcp_server.py
# MCP Server 运行在 http://localhost:8651
```

### 3. 配置 AI 客户端

在 Claude Desktop 或 Cursor 中添加 MCP 配置：

```json
{
  "mcpServers": {
    "dotnetmcp": {
      "url": "http://localhost:8651/mcp"
    }
  }
}
```

## 🛠️ MCP 工具

### 分析工具
| 工具 | 描述 |
|-----|------|
| `get_assembly_info` | 获取程序集信息（推荐首次调用） |
| `get_type_source` | 获取类型源码 |
| `get_type_info` | 类型结构（继承、接口、成员） |
| `search_types_by_keyword` | 搜索类型 |
| `get_xrefs_to_*` | 交叉引用 |
| `build_call_graph` | 调用图 |

### 修改工具
| 工具 | 描述 |
|-----|------|
| `begin_modify_session` | 开始修改会话 |
| `replace_method_body` | 替换方法体（C# 或 IL） |
| `inject_il` | 注入 IL 指令 |
| `commit_session` | 提交修改 |
| `rollback_session` | 回滚修改 |

### 实例管理
| 工具 | 描述 |
|-----|------|
| `list_instances` | 列出实例 |
| `get_analysis_status` | 分析状态（索引、内存） |
| `clear_cache` | 清除缓存 |

## 📚 MCP Resources

| URI | 描述 |
|-----|------|
| `dotnetmcp://usage-guide` | 使用指南 |
| `dotnetmcp://decision-matrix` | 工具决策矩阵 |
| `dotnetmcp://capabilities` | 当前能力列表 |

## 🎯 MCP Prompts

| Prompt | 描述 |
|--------|------|
| `status-check` | 状态检查流程 |
| `analyze-type` | 类型分析流程 |
| `patch-method` | 方法修改流程 |
| `find-vulnerability` | 安全审计流程 |

## ⚙️ 配置

编辑 `mcp-server/data/config/server.toml`：

```toml
[server]
transport = "http"
port = 8651

[backend]
host = "127.0.0.1"
port = 8650

[security]
allow_dynamic_instances = false

[[users]]
name = "admin"
token = "your-secret-token"
is_admin = true
```

## 🐳 Docker 部署

```bash
docker-compose -f docker/docker-compose.yml up -d
```

## 📖 文档

- [开发指南](DEVELOPMENT.md) - 详细开发规范和阶段规划
- [AI 开发指南](AGENTS.md) - AI 辅助开发规范

## 🎯 目标平台

| 平台 | 支持 |
|-----|------|
| .NET Framework 2.0-4.8.x | ✅ |
| .NET Core 1.0-3.1 | ✅ |
| .NET 5+ | ✅ |
| Unity IL2CPP | ❌ |
| AOT 编译产物 | ❌ |

## 📄 许可证

MIT License

## 🙏 致谢

- [jadx-ai-mcp](https://github.com/xjoker/jadx-ai-mcp) - 架构参考
- [Mono.Cecil](https://github.com/jbevain/cecil) - 元数据读写
- [ILSpy](https://github.com/icsharpcode/ILSpy) - 反编译引擎
- [FastMCP](https://github.com/jlowin/fastmcp) - MCP Python SDK
