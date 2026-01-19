# DotNet MCP - AI 静态逆向工程 MCP 服务

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![Python](https://img.shields.io/badge/Python-3.12+-3776AB)](https://python.org/)
[![MCP](https://img.shields.io/badge/MCP-Protocol-orange)](https://modelcontextprotocol.io/)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)
[![Tests](https://img.shields.io/badge/Tests-70%2B%20Passing-brightgreen)]()

> 为 AI 提供 .NET 托管代码静态逆向工程能力的 MCP 服务

## 🎯 项目概述

DotNet MCP 是一个专为 AI 设计的 .NET 程序集分析与修改服务，采用 MCP (Model Context Protocol) 协议，使 LLM 能够直接分析和修改 .NET 托管代码。

### 核心能力

| 类别 | 功能 |
|-----|------|
| **元数据读取** | 程序集、类型、方法、属性、字段信息 |
| **反编译** | IL → C# 源码（ILSpy 引擎） |
| **搜索** | 类型/方法/字符串全文搜索 |
| **交叉引用** | 调用图、引用追踪 |
| **编译** | C# 源码 → 程序集（Roslyn） |

### 支持平台

✅ .NET Framework 2.0-4.8.x | ✅ .NET Core 1.0-3.1 | ✅ .NET 5-10+  
✅ .NET Standard | ✅ Mono | ✅ Xamarin/MAUI  
❌ IL2CPP | ❌ NativeAOT

---

## 🏗️ 架构

```
┌─────────────────┐      HTTP/REST      ┌──────────────────────┐
│   AI / LLM      │◄───────────────────►│   Python MCP Server  │
│  (Claude, etc)  │      MCP Protocol   │   (FastMCP 2.0+)     │
└─────────────────┘                     │   Port: 8651         │
                                        └──────────┬───────────┘
                                                   │
                                              HTTP/REST
                                                   │
                                        ┌──────────▼───────────┐
                                        │   C# Backend Service │
                                        │   (ASP.NET Core 9.0) │
                                        │   Port: 8650         │
                                        ├──────────────────────┤
                                        │  • Mono.Cecil        │
                                        │  • ILSpy 9.1         │
                                        │  • Roslyn 5.0        │
                                        └──────────────────────┘
```

---

## 📦 后端服务架构

### Core 模块

| 模块 | 说明 | 测试覆盖 |
|-----|------|---------|
| **Context** | 程序集加载与上下文管理 | ✅ 12 tests |
| **Identity** | MemberId/LocationId 编解码 | ✅ 20 tests |
| **Paging** | 游标分页与数据切片 | ✅ 27 tests |
| **Compilation** | Roslyn C# 编译服务 | ✅ 11 tests |

### 关键类

```
Core/
├── Context/
│   ├── AssemblyContext.cs      # 程序集加载、生命周期管理
│   └── CustomAssemblyResolver.cs # 三级依赖解析策略
├── Identity/
│   ├── MemberIdCodec.cs        # {mvid}:{token}:{kind}
│   ├── LocationIdCodec.cs      # {memberId}@{offset}
│   ├── SignatureBuilder.cs     # 泛型签名构建
│   └── MemberIdGenerator.cs    # Cecil 成员 → ID
├── Paging/
│   ├── CursorCodec.cs          # Base64 游标编解码
│   ├── PagingService.cs        # 游标分页 (50/500)
│   └── SlicingService.cs       # 数据切片/批量
└── Compilation/
    ├── CompilationService.cs   # C# 源码编译
    └── ReferenceAssemblyProvider.cs # 引用程序集管理
```

---

## 🚀 快速开始

### 前置条件

- Python >= 3.12 (推荐 3.14)
- .NET SDK 9.0
- Docker（可选，用于部署）

### 1. 启动后端服务

```bash
cd backend-service/src/DotNetMcp.Backend
dotnet run
# 服务启动于 http://localhost:8650
```

### 2. 启动 MCP Server

```bash
cd mcp-server
pip install -r requirements.txt
python dotnetmcp_server.py
# MCP 服务启动于 http://localhost:8651
```

### 3. 配置 AI 客户端

```json
{
  "mcpServers": {
    "dotnetmcp": {
      "url": "http://localhost:8651/mcp/v1",
      "transport": "streamable-http"
    }
  }
}
```

---

## 🧪 测试

```bash
cd backend-service
dotnet test
```

**当前测试状态**：
- ✅ 70+ 单元测试
- ✅ 4 集成测试
- ✅ 100% 核心模块覆盖

---

## 📖 API 参考

### MemberId 格式

```
{mvid}:{token}:{kind}

示例: a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6:06001234:M

- mvid: 模块版本 ID (32 字符十六进制)
- token: 元数据 Token (8 字符十六进制)
- kind: 成员类型 (T=Type, M=Method, F=Field, P=Property, E=Event)
```

### LocationId 格式

```
{memberId}@{offset}

示例: a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6:06001234:M@001A

- offset: IL 偏移量 (4 字符十六进制)
```

### REST API 端点

| 端点 | 方法 | 说明 |
|-----|------|------|
| `/assembly/load` | POST | 加载程序集 |
| `/assembly/info` | GET | 获取程序集信息 |
| `/health` | GET | 健康检查 |

---

## � 技术栈

| 组件 | 版本 | 用途 |
|-----|------|------|
| .NET SDK | 9.0 | 后端运行时 |
| Mono.Cecil | 0.11.6 | 元数据读写 |
| ILSpy | 9.1.0 | 反编译引擎 |
| Roslyn | 5.0.0 | C# 编译器 |
| FastMCP | 2.0+ | MCP 协议 |
| httpx | 0.28+ | HTTP 客户端 |

---

## 📁 项目结构

```
DotNetMCP/
├── backend-service/           # C# 后端服务
│   ├── src/DotNetMcp.Backend/ # 主项目
│   └── tests/                 # 单元/集成测试
├── mcp-server/                # Python MCP 服务
│   ├── dotnetmcp_server.py    # 入口
│   └── src/server/            # 服务模块
├── docker/                    # Docker 配置
├── DEVELOPMENT.md             # 开发指南
├── TECH_STACK.md              # 技术栈详情
└── AGENTS.md                  # AI 开发指南
```

---

## � 许可证

MIT License
