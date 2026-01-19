# DotNet MCP 开发指导文档

> AI 静态逆向工程 MCP 服务 - 完整开发规范

**版本**: 1.0  
**最后更新**: 2026-01-19  
**参考项目**: [jadx-ai-mcp](https://github.com/xjoker/jadx-ai-mcp)

---

## 目录

1. [项目概述](#1-项目概述)
2. [技术栈](#2-技术栈)
3. [项目架构](#3-项目架构)
4. [开发阶段规划](#4-开发阶段规划)
5. [功能模块详解](#5-功能模块详解)
6. [MCP 协议实现](#6-mcp-协议实现)
7. [工具设计规范](#7-工具设计规范)
8. [配置与部署](#8-配置与部署)
9. [测试策略](#9-测试策略)
10. [开发规范](#10-开发规范)

---

## 1. 项目概述

### 1.1 项目目标

构建一套 MCP（Model Context Protocol）服务，使 AI 能够：
- 自主进行 .NET 程序集的静态逆向分析
- 精确定位和引用代码实体（通过稳定 ID 系统）
- 高效管理分析上下文（分页、切片）
- 自动化代码修改（方法替换、IL 注入）

### 1.2 目标平台

| 平台 | 版本 | 支持程度 |
|-----|------|---------|
| .NET Framework | 2.0 - 4.8.x | ✅ 完整 |
| .NET Core | 1.0 - 3.1 | ✅ 完整 |
| .NET 5+ | 5.0 - 9.0+ | ✅ 完整 |
| Unity IL2CPP | - | ❌ 不支持 |
| AOT 编译产物 | - | ❌ 不支持 |

### 1.3 核心设计原则

| 原则 | 说明 |
|-----|------|
| **可复现** | 相同输入产生相同输出 |
| **可追溯** | 每个结论可追溯到代码位置 |
| **成本可控** | 单次操作上下文消耗有上限 |
| **修改可验证** | 所有修改经过 IL 验证 |
| **修改可回滚** | 支持事务性回滚 |

---

## 2. 技术栈

### 2.1 架构方案选择

基于 jadx-ai-mcp 的实践经验，提供两种架构方案：

| 方案 | 优点 | 缺点 | 推荐场景 |
|-----|------|------|---------|
| **方案 A: 分离式** | 生态成熟、易维护、参考成熟 | 多语言、进程间通信 | ⭐ 推荐 |
| 方案 B: 一体化 C# | 单一语言、无 IPC 开销 | SDK 较新、参考少 | 纯 .NET 团队 |

### 2.2 方案 A: 分离式架构（推荐）⭐

> 参考 jadx-ai-mcp：Python MCP Server + Java JADX Plugin

```
┌─────────────────────────────────────────────────────────────┐
│                     AI 客户端 (Claude/Cursor)                │
└─────────────────────────────────────────────────────────────┘
                              │ MCP 协议
                              ▼
┌─────────────────────────────────────────────────────────────┐
│              Python MCP Server (FastMCP)                    │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐    │
│  │ 工具定义  │  │ Prompts  │  │Resources │  │ 认证中间件│    │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘    │
│                        端口: 8651                           │
└─────────────────────────────────────────────────────────────┘
                              │ HTTP REST API
                              ▼
┌─────────────────────────────────────────────────────────────┐
│              C# 后端服务 (ASP.NET Core Web API)             │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐    │
│  │Mono.Cecil│  │反编译引擎 │  │修改引擎  │  │ IL 验证  │    │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘    │
│                        端口: 8650                           │
└─────────────────────────────────────────────────────────────┘
```

**技术栈**：

| 层 | 语言 | 框架/库 | 版本 |
|---|------|--------|------|
| MCP Server | Python | FastMCP | >= 2.0 |
| HTTP Client | Python | httpx | >= 0.28 |
| 后端 API | C# | ASP.NET Core | 10.0 |
| 元数据读写 | C# | Mono.Cecil | 0.11.6 |
| 反编译 | C# | ICSharpCode.Decompiler | 9.1.0 |
| C# 编译 | C# | Roslyn | 5.0.0 |

**目录结构**：

```
DotNetMCP/
├── mcp-server/                    # Python MCP Server
│   ├── src/
│   │   ├── server/
│   │   │   ├── __init__.py
│   │   │   ├── config.py          # 配置管理
│   │   │   ├── config_loader.py   # TOML 加载
│   │   │   ├── auth_middleware.py # Token 认证
│   │   │   ├── instance_registry.py # 实例注册表
│   │   │   ├── prompts.py         # MCP Prompts
│   │   │   ├── resources.py       # MCP Resources
│   │   │   └── tools/             # MCP 工具
│   │   │       ├── analysis.py
│   │   │       ├── modification.py
│   │   │       ├── instance.py
│   │   │       └── batch.py
│   │   └── utils/
│   │       └── pagination.py
│   ├── data/config/
│   │   └── server.toml
│   ├── pyproject.toml
│   └── requirements.txt
│
├── backend-service/               # C# 后端服务
│   ├── src/
│   │   └── DotNetMcp.Backend/
│   │       ├── Controllers/       # REST API
│   │       │   ├── AssemblyController.cs
│   │       │   ├── AnalysisController.cs
│   │       │   └── ModifyController.cs
│   │       ├── Services/          # 业务逻辑
│   │       │   ├── AssemblyService.cs
│   │       │   ├── DecompileService.cs
│   │       │   ├── IndexService.cs
│   │       │   └── ModifyService.cs
│   │       ├── Core/              # 核心功能
│   │       │   ├── Identity/
│   │       │   ├── Validation/
│   │       │   └── Compile/
│   │       └── Program.cs
│   ├── tests/
│   └── DotNetMcp.Backend.sln
│
├── docker/
│   ├── Dockerfile.mcp-server
│   ├── Dockerfile.backend
│   └── docker-compose.yml
│
├── docs/
├── DEVELOPMENT.md
└── README.md
```

### 2.3 方案 B: 一体化 C# 架构

> 使用 Microsoft 官方 MCP SDK

```
┌─────────────────────────────────────────────────────────────┐
│                     AI 客户端 (Claude/Cursor)                │
└─────────────────────────────────────────────────────────────┘
                              │ MCP 协议
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                C# MCP Server + 后端服务一体化                │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ MCP Layer (ModelContextProtocol.AspNetCore)            │ │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐             │ │
│  │  │ [McpTool]│  │ Prompts  │  │Resources │             │ │
│  │  └──────────┘  └──────────┘  └──────────┘             │ │
│  └────────────────────────────────────────────────────────┘ │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ Backend Layer                                          │ │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐             │ │
│  │  │Mono.Cecil│  │ Decompile│  │  Modify  │             │ │
│  │  └──────────┘  └──────────┘  └──────────┘             │ │
│  └────────────────────────────────────────────────────────┘ │
│                        端口: 8651                           │
└─────────────────────────────────────────────────────────────┘
```

**技术栈**：

| 组件 | 包名 | 版本 |
|-----|------|------|
| MCP Core | ModelContextProtocol | latest |
| MCP ASP.NET | ModelContextProtocol.AspNetCore | latest |
| 元数据读写 | Mono.Cecil | >= 0.11.5 |
| 反编译 | ICSharpCode.Decompiler | >= 8.0 |

**NuGet 依赖**：
```xml
<PackageReference Include="ModelContextProtocol.AspNetCore" Version="*" />
<PackageReference Include="Mono.Cecil" Version="0.11.5" />
<PackageReference Include="ICSharpCode.Decompiler" Version="8.0.0" />
```

### 2.4 方案对比

| 维度 | 方案 A: 分离式 | 方案 B: 一体化 |
|-----|---------------|---------------|
| **MCP 生态兼容性** | ⭐⭐⭐ FastMCP 成熟 | ⭐⭐ SDK 较新 |
| **参考项目** | jadx-ai-mcp 直接参考 | 无直接参考 |
| **开发复杂度** | 中（双语言） | 低（单语言） |
| **运维复杂度** | 中（两个进程） | 低（单进程） |
| **性能** | 有 HTTP 开销 | 无 IPC 开销 |
| **扩展性** | 高（独立扩展） | 中 |
| **Docker 部署** | 两个容器或 All-in-One | 单容器 |

### 2.5 推荐决策

**推荐方案 A（分离式）**，理由：

1. **成熟度**：FastMCP 已在 jadx-ai-mcp 中验证
2. **可参考**：可直接复用 jadx-ai-mcp 的 Python 代码
3. **关注点分离**：MCP 协议层与业务逻辑解耦
4. **独立演进**：后端 API 可被其他客户端复用

---

## 3. 项目架构（方案 A: 分离式）

```
DotNetMCP/
├── src/
│   └── DotNetMcp/
│       ├── Core/                      # 核心服务
│       │   ├── Identity/              # ID 编解码
│       │   │   ├── MemberIdCodec.cs
│       │   │   ├── LocationIdCodec.cs
│       │   │   └── SignatureBuilder.cs
│       │   ├── Index/                 # 索引服务
│       │   │   ├── SymbolIndex.cs
│       │   │   ├── StringIndex.cs
│       │   │   └── XRefIndex.cs
│       │   ├── Decompile/             # 反编译服务
│       │   │   ├── DecompileService.cs
│       │   │   └── ILDisassembler.cs
│       │   ├── Modify/                # 修改服务
│       │   │   ├── ModifySession.cs
│       │   │   ├── MethodBodyReplacer.cs
│       │   │   └── ILInjector.cs
│       │   └── Compile/               # Roslyn 编译
│       │       ├── CSharpCompiler.cs
│       │       └── MethodBodyExtractor.cs
│       │
│       ├── Infrastructure/            # 基础设施
│       │   ├── Auth/                  # 认证
│       │   │   ├── AuthMiddleware.cs
│       │   │   └── UserContext.cs
│       │   ├── Registry/              # 实例注册表
│       │   │   ├── AssemblyInstance.cs
│       │   │   └── AssemblyRegistry.cs
│       │   ├── Paging/                # 分页
│       │   │   ├── CursorCodec.cs
│       │   │   └── PagingService.cs
│       │   ├── Slicing/               # 切片
│       │   │   └── SlicingService.cs
│       │   ├── Transaction/           # 事务
│       │   │   ├── TransactionManager.cs
│       │   │   └── ModifyQueue.cs
│       │   ├── Validation/            # 验证
│       │   │   ├── ILValidator.cs
│       │   │   ├── MetadataValidator.cs
│       │   │   └── ValidationResult.cs
│       │   ├── Context/               # 上下文
│       │   │   ├── AssemblyContext.cs
│       │   │   └── CustomAssemblyResolver.cs
│       │   └── Cache/                 # 缓存
│       │       └── CacheManager.cs
│       │
│       ├── Tools/                     # MCP 工具
│       │   ├── Analysis/              # 分析工具
│       │   │   ├── GetAssemblyInfo.cs
│       │   │   ├── GetTypeSource.cs
│       │   │   ├── SearchMembers.cs
│       │   │   ├── FindUsages.cs
│       │   │   └── BuildCallGraph.cs
│       │   ├── Modification/          # 修改工具
│       │   │   ├── BeginModifySession.cs
│       │   │   ├── ReplaceMethodBody.cs
│       │   │   ├── InjectIL.cs
│       │   │   └── CommitSession.cs
│       │   ├── Instance/              # 实例管理
│       │   │   ├── ListInstances.cs
│       │   │   ├── AddInstance.cs
│       │   │   └── GetAnalysisStatus.cs
│       │   └── Batch/                 # 批量工具
│       │       ├── BatchGetTypeSource.cs
│       │       └── BatchFindUsages.cs
│       │
│       ├── Mcp/                       # MCP 协议
│       │   ├── Server.cs              # 服务器入口
│       │   ├── ToolRegistry.cs        # 工具注册
│       │   ├── HttpTransport.cs       # HTTP 传输
│       │   ├── StdioTransport.cs      # stdio 传输
│       │   ├── Prompts/               # MCP Prompts
│       │   │   └── PromptRegistry.cs
│       │   ├── Resources/             # MCP Resources
│       │   │   └── ResourceRegistry.cs
│       │   └── Schemas/               # JSON Schema
│       │       └── ToolSchemas.cs
│       │
│       ├── Models/                    # 数据模型
│       │   ├── MemberId.cs
│       │   ├── LocationId.cs
│       │   ├── ToolResult.cs
│       │   └── ErrorCodes.cs
│       │
│       └── Program.cs                 # 入口
│
├── tests/
│   ├── DotNetMcp.Tests/                   # 单元测试
│   └── DotNetMcp.IntegrationTests/        # 集成测试
│
├── samples/                           # 测试程序集
├── data/config/                       # TOML 配置
├── docker/
├── docs/                              # 文档
│   ├── getting-started/
│   ├── reference/
│   ├── deployment/
│   └── troubleshooting/
│
├── DEVELOPMENT.md                     # 本文档
├── AGENTS.md                          # AI 开发指南
├── README.md
├── .env.example
├── Dockerfile
└── DotNetMcp.sln
```

### 3.2 分层架构图

```
┌─────────────────────────────────────────────────────────────┐
│                     AI 客户端 (Claude/Cursor)                │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    MCP 协议层 (Mcp/)                         │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐    │
│  │ HTTP传输  │  │ stdio传输 │  │ Prompts  │  │Resources │    │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘    │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    工具层 (Tools/)                           │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐    │
│  │ Analysis │  │Modification│ │ Instance │  │  Batch   │    │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘    │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    核心服务层 (Core/)                        │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐    │
│  │ Identity │  │  Index   │  │Decompile │  │  Modify  │    │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘    │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                  基础设施层 (Infrastructure/)                │
│  ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐    │
│  │  Auth  │ │Registry│ │ Paging │ │Validate│ │ Cache  │    │
│  └────────┘ └────────┘ └────────┘ └────────┘ └────────┘    │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    外部依赖                                  │
│  ┌──────────────────────┐  ┌──────────────────────┐        │
│  │     Mono.Cecil       │  │  ICSharpCode.Decompiler │      │
│  └──────────────────────┘  └──────────────────────┘        │
└─────────────────────────────────────────────────────────────┘
```

---

## 4. 开发阶段规划

### Phase 1: 基础设施层 (7 周)

#### Week 1-2: Cecil 集成与上下文管理

| 任务 | 优先级 | 预估时间 | 交付物 |
|-----|-------|---------|-------|
| 创建解决方案和项目结构 | P0 | 2h | `DotNetMcp.sln` |
| 实现 `AssemblyContext` | P0 | 8h | 程序集加载/卸载 |
| 实现 `CustomAssemblyResolver` | P0 | 6h | 依赖解析 |
| 单元测试 | P1 | 4h | 测试覆盖 |

**验收标准**：
- [ ] 可以加载任意 .NET 程序集
- [ ] 依赖解析成功率 > 90%
- [ ] 内存泄漏测试通过

#### Week 3: ID 系统

| 任务 | 优先级 | 预估时间 | 交付物 |
|-----|-------|---------|-------|
| 实现 `MemberIdCodec` | P0 | 6h | MemberId 编解码 |
| 实现 `LocationIdCodec` | P0 | 4h | LocationId 编解码 |
| 实现 `SignatureBuilder` | P0 | 6h | 泛型签名规范化 |
| 实现 ID 映射表生成 | P1 | 4h | 修改后 ID 映射 |

**验收标准**：
- [ ] 同一 DLL 重启后 MemberId 不变
- [ ] 泛型类型 ID 确定性可验证
- [ ] 修改后映射表正确

#### Week 4: 分页与切片

| 任务 | 优先级 | 预估时间 | 交付物 |
|-----|-------|---------|-------|
| 实现 `CursorCodec` | P0 | 3h | 游标编解码 |
| 实现 `PagingService` | P0 | 4h | 分页遍历 |
| 实现 `SlicingService` | P0 | 6h | 代码切片 |
| 版本绑定机制 | P1 | 3h | 游标失效检测 |

**验收标准**：
- [ ] 分页遍历结果完整一致
- [ ] 切片内容哈希可验证
- [ ] 索引更新后游标正确失效

#### Week 5-6: Roslyn 编译集成

| 任务 | 优先级 | 预估时间 | 交付物 |
|-----|-------|---------|-------|
| 实现 `CSharpCompiler` | P0 | 12h | C# 代码编译 |
| 实现 `MethodBodyExtractor` | P0 | 8h | 提取编译后方法体 |
| 上下文感知编译 | P1 | 8h | 自动解析引用 |
| 错误处理与诊断 | P1 | 4h | 编译错误报告 |

**验收标准**：
- [ ] 可编译简单 C# 方法体
- [ ] 自动解析目标程序集引用
- [ ] 编译错误信息清晰

#### Week 7: 测试与文档

| 任务 | 优先级 | 预估时间 | 交付物 |
|-----|-------|---------|-------|
| 单元测试补全 | P0 | 16h | 覆盖率 > 80% |
| 集成测试 | P1 | 8h | 端到端流程 |
| 文档编写 | P1 | 8h | API 文档 |

---

### Phase 2: 分析能力 (6 周)

#### Week 1-2: 索引服务

| 任务 | 优先级 | 预估时间 | 交付物 |
|-----|-------|---------|-------|
| 实现 `SymbolIndex` | P0 | 12h | 符号索引 |
| 实现 `StringIndex` | P0 | 6h | 字符串索引 |
| 实现 `XRefIndex` | P0 | 12h | 交叉引用索引 |
| 增量索引更新 | P1 | 6h | 修改后增量更新 |

#### Week 3: 搜索工具

| 任务 | 优先级 | 预估时间 | 交付物 |
|-----|-------|---------|-------|
| `search_types_by_keyword` | P0 | 4h | 类型搜索 |
| `search_method_by_name` | P0 | 4h | 方法搜索 |
| `search_string_literals` | P0 | 4h | 字符串搜索 |
| 过滤器支持 | P1 | 4h | 命名空间/可见性过滤 |

#### Week 4: 源码工具

| 任务 | 优先级 | 预估时间 | 交付物 |
|-----|-------|---------|-------|
| `get_type_source` | P0 | 6h | 类型源码 |
| `get_method_by_name` | P0 | 4h | 方法源码 |
| `get_il_source` | P0 | 4h | IL 反汇编 |
| 行号映射 | P1 | 4h | IL 到源码映射 |

#### Week 5: 交叉引用

| 任务 | 优先级 | 预估时间 | 交付物 |
|-----|-------|---------|-------|
| `get_xrefs_to_type` | P0 | 4h | 类型引用 |
| `get_xrefs_to_method` | P0 | 4h | 方法引用 |
| `get_xrefs_to_field` | P0 | 4h | 字段引用 |
| 使用类型分类 | P1 | 4h | 调用/继承/实现等 |

#### Week 6: 调用图

| 任务 | 优先级 | 预估时间 | 交付物 |
|-----|-------|---------|-------|
| `build_call_graph` | P0 | 8h | 调用图构建 |
| 深度限制 | P0 | 2h | 防止无限递归 |
| 节点过滤 | P1 | 4h | 排除系统方法 |

---

### Phase 3: 修改能力 (9 周)

#### Week 1: 会话管理

| 任务 | 优先级 | 预估时间 | 交付物 |
|-----|-------|---------|-------|
| `begin_modify_session` | P0 | 6h | 创建修改会话 |
| `commit_session` | P0 | 6h | 提交修改 |
| `rollback_session` | P0 | 4h | 回滚修改 |
| `TransactionManager` | P0 | 8h | 事务管理 |

#### Week 2-4: 方法修改

| 任务 | 优先级 | 预估时间 | 交付物 |
|-----|-------|---------|-------|
| `replace_method_body` (IL) | P0 | 16h | IL 格式替换 |
| `replace_method_body` (C#) | P0 | 16h | C# 编译替换 |
| `inject_il` | P0 | 12h | IL 注入 |
| `wrap_method` | P1 | 8h | 方法包装 |
| 分支目标更新 | P0 | 8h | 自动调整跳转 |

#### Week 5-6: 成员操作

| 任务 | 优先级 | 预估时间 | 交付物 |
|-----|-------|---------|-------|
| `add_member` | P0 | 12h | 添加成员 |
| `remove_member` | P0 | 8h | 删除成员 |
| `rename_member` | P1 | 8h | 重命名 |
| 引用更新 | P1 | 8h | 自动更新引用 |

#### Week 7: 特性操作

| 任务 | 优先级 | 预估时间 | 交付物 |
|-----|-------|---------|-------|
| `add_attribute` | P0 | 6h | 添加特性 |
| `remove_attribute` | P0 | 4h | 删除特性 |
| 特性参数解析 | P1 | 6h | 构造函数/命名参数 |

#### Week 8: 验证系统

| 任务 | 优先级 | 预估时间 | 交付物 |
|-----|-------|---------|-------|
| `ILValidator` | P0 | 12h | IL 验证 |
| `MetadataValidator` | P0 | 8h | 元数据验证 |
| 验证报告 | P1 | 4h | 详细错误信息 |

#### Week 9: 输出与补丁

| 任务 | 优先级 | 预估时间 | 交付物 |
|-----|-------|---------|-------|
| `write_assembly` | P0 | 6h | 写入程序集 |
| `generate_patch` | P1 | 8h | 生成差异补丁 |
| ID 映射表 | P0 | 4h | 新旧 ID 映射 |

---

### Phase 4: MCP 集成 (5 周)

#### Week 1-2: MCP 服务器

| 任务 | 优先级 | 预估时间 | 交付物 |
|-----|-------|---------|-------|
| `HttpTransport` | P0 | 12h | Streamable HTTP |
| `StdioTransport` | P0 | 6h | stdio 模式 |
| `ToolRegistry` | P0 | 8h | 工具注册 |
| `AuthMiddleware` | P0 | 6h | Token 认证 |

#### Week 3: MCP Resources & Prompts ⭐

| 任务 | 优先级 | 预估时间 | 交付物 |
|-----|-------|---------|-------|
| `dotnetmcp://usage-guide` | P0 | 4h | 使用指南 |
| `dotnetmcp://decision-matrix` | P0 | 4h | 决策矩阵 |
| `dotnetmcp://capabilities` | P0 | 4h | 能力列表 |
| `status-check` Prompt | P0 | 2h | 状态检查流程 |
| `analyze-type` Prompt | P0 | 2h | 类型分析流程 |
| `patch-method` Prompt | P0 | 2h | 方法修改流程 |

#### Week 4: AI 行为测试

| 任务 | 优先级 | 预估时间 | 交付物 |
|-----|-------|---------|-------|
| ID 使用正确性测试 | P0 | 8h | 测试用例 |
| 分页使用测试 | P1 | 6h | 测试用例 |
| 错误恢复测试 | P1 | 6h | 测试用例 |
| 修改流程测试 | P0 | 8h | 测试用例 |

#### Week 5: 文档与 Docker

| 任务 | 优先级 | 预估时间 | 交付物 |
|-----|-------|---------|-------|
| 完整文档 | P0 | 16h | docs/ |
| Docker 镜像 | P0 | 8h | Dockerfile |
| docker-compose | P1 | 4h | 多实例部署 |
| README 完善 | P0 | 4h | 快速开始 |

---

## 5. 功能模块详解

### 5.1 ID 系统

#### MemberId 格式
```
格式: <mvid>:<token>:<kind>

示例: a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6:06001234:M

mvid  : 32 位十六进制，模块版本 ID
token : 8 位十六进制，ECMA-335 元数据 Token
kind  : T=Type, M=Method, F=Field, P=Property, E=Event, N=Namespace
```

#### LocationId 格式
```
格式: <memberId>@<ilOffset>

示例: a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6:06001234:M@0x001A
```

### 5.2 多用户架构

#### 实例注册表

```csharp
public class AssemblyInstance
{
    public string Name { get; set; }
    public string Path { get; set; }
    public string Status { get; set; }     // connected | pending | error
    public string? Owner { get; set; }     // null = 共享
    public bool IsDynamic { get; set; }
    public DateTime LastAccess { get; set; }
}

public class AssemblyRegistry
{
    public Task<Result> AddInstanceAsync(string path, string? owner, bool isDynamic);
    public AssemblyInstance? GetInstance(string name, string currentUser, bool isAdmin);
    public IEnumerable<AssemblyInstance> ListInstances(string currentUser, bool isAdmin);
}
```

#### 访问控制

| 操作 | 普通用户 | Admin |
|-----|---------|-------|
| 访问共享实例 | ✅ | ✅ |
| 访问自己的动态实例 | ✅ | ✅ |
| 访问他人的动态实例 | ❌ | ✅ |
| 添加动态实例 | 需权限 | ✅ |
| 修改程序集 | 仅自己的 | 所有 |

---

## 6. MCP 协议实现

### 6.1 传输模式

#### Streamable HTTP（生产环境）
```
Endpoint: POST /mcp
Content-Type: application/json
Authorization: Bearer <token>

可选 SSE 升级用于服务端推送
```

#### stdio（本地开发）
```
stdin/stdout 双向通信
每行一个 JSON-RPC 消息
```

### 6.2 MCP Resources

| URI | 描述 |
|-----|------|
| `dotnetmcp://usage-guide` | 工具使用指南，AI 首次使用时阅读 |
| `dotnetmcp://decision-matrix` | 决策矩阵，帮助 AI 选择正确工具 |
| `dotnetmcp://capabilities` | 当前实例能力列表 |

### 6.3 MCP Prompts

| Prompt | 描述 |
|--------|------|
| `status-check` | 在执行耗资源操作前检查状态 |
| `analyze-type` | 标准类型分析流程 |
| `find-vulnerability` | 安全审计流程 |
| `patch-method` | 方法修改标准流程 |

---

## 7. 工具设计规范

### 7.1 工具分类

| 分级 | 特点 | 工具示例 |
|-----|------|---------|
| **Fast** | 无需索引，立即响应 | `list_instances`, `get_assembly_info` |
| **Moderate** | 需要索引，毫秒级 | `search_types`, `get_type_source` |
| **Slow** | 需要深度分析 | `build_call_graph`, `replace_method_body` |

### 7.2 工具列表

#### 分析工具 (15 个)

| 工具 | 分级 | 描述 |
|-----|------|------|
| `get_assembly_info` | Fast | 获取程序集信息 |
| `get_type_source` | Moderate | 获取类型源码 |
| `get_method_by_name` | Moderate | 获取方法源码 |
| `get_il_source` | Fast | 获取 IL 反汇编 |
| `get_type_info` | Fast | 类型结构信息 |
| `get_methods_of_type` | Fast | 类型的所有方法 |
| `get_fields_of_type` | Fast | 类型的所有字段 |
| `get_all_types` | Moderate | 所有类型（分页） |
| `search_types_by_keyword` | Moderate | 搜索类型 |
| `search_method_by_name` | Moderate | 搜索方法 |
| `search_string_literals` | Moderate | 搜索字符串 |
| `get_xrefs_to_type` | Moderate | 类型引用 |
| `get_xrefs_to_method` | Moderate | 方法引用 |
| `get_xrefs_to_field` | Moderate | 字段引用 |
| `build_call_graph` | Slow | 调用图 |

#### 批量工具 (3 个)

| 工具 | 分级 | 描述 | 限制 |
|-----|------|------|------|
| `batch_get_type_source` | Moderate | 批量获取源码 | max=20 |
| `batch_get_method_by_name` | Moderate | 批量获取方法 | max=20 |
| `batch_get_xrefs` | Moderate | 批量获取引用 | max=10 |

#### 修改工具 (10 个)

| 工具 | 分级 | 描述 |
|-----|------|------|
| `begin_modify_session` | Fast | 开始修改会话 |
| `replace_method_body` | Slow | 替换方法体 |
| `inject_il` | Slow | 注入 IL |
| `wrap_method` | Slow | 包装方法 |
| `add_member` | Slow | 添加成员 |
| `remove_member` | Slow | 删除成员 |
| `add_attribute` | Slow | 添加特性 |
| `remove_attribute` | Slow | 删除特性 |
| `commit_session` | Slow | 提交修改 |
| `rollback_session` | Fast | 回滚修改 |

#### 实例管理工具 (7 个)

| 工具 | 分级 | 描述 |
|-----|------|------|
| `list_instances` | Fast | 列出实例 |
| `add_instance` | Fast | 添加实例 |
| `remove_instance` | Fast | 删除实例 |
| `set_default_instance` | Fast | 设置默认 |
| `get_instance_info` | Fast | 实例详情 |
| `get_analysis_status` | Fast | 分析状态 |
| `clear_cache` | Fast | 清除缓存 |

### 7.3 性能决策矩阵

| 状态字段 | 阈值 | 推荐动作 |
|---------|------|---------|
| `index.percentage < 20%` | 低索引 | 仅使用元数据查询 |
| `index.percentage > 50%` | 良好索引 | 可使用全文搜索 |
| `memory.usage > 85%` | 内存紧张 | 减少批量大小为 5 |
| `sessions.modify_count > 3` | 修改会话多 | 等待或提示用户 |

---

## 8. 配置与部署

### 8.1 配置文件

```toml
# data/config/server.toml

[server]
transport = "http"  # http | stdio
port = 8651
log_level = "info"

[security]
allow_dynamic_instances = false

[[users]]
name = "alice"
token = "token-alice-xxxxx"

[[users]]
name = "admin"
token = "token-admin-zzzzz"
is_admin = true

[[assembly_instances]]
name = "shared-lib"
path = "/data/assemblies/SharedLib.dll"
default = true
```

### 8.2 Docker 部署

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app
COPY publish/ .

ENV DOTNETMCP_TRANSPORT=http
ENV DOTNETMCP_PORT=8651

EXPOSE 8651
ENTRYPOINT ["dotnet", "DotNetMcp.dll"]
```

### 8.3 docker-compose

```yaml
version: '3.8'
services:
  dotnetmcp:
    image: dotnetmcp:latest
    ports:
      - "8651:8651"
    volumes:
      - ./data/config:/app/data/config:ro
      - ./assemblies:/data/assemblies:ro
      - dotnetmcp-cache:/app/cache
    environment:
      - DOTNETMCP_AUTH_TOKEN=your-secret-token

volumes:
  dotnetmcp-cache:
```

---

## 9. 测试策略

### 9.1 单元测试

| 模块 | 测试重点 |
|-----|---------|
| Identity | ID 编解码正确性、泛型签名 |
| Paging | 游标编解码、版本绑定 |
| Validation | IL 验证准确性 |
| Compile | C# 编译正确性 |

### 9.2 集成测试

| 场景 | 验证点 |
|-----|-------|
| 完整分析流程 | 加载→搜索→反编译→交叉引用 |
| 完整修改流程 | 会话→修改→验证→提交 |
| 事务回滚 | 修改后回滚，状态恢复 |

### 9.3 AI 行为测试

| 测试 | 验证点 |
|-----|-------|
| ID 使用 | AI 正确使用 MemberId 引用 |
| 分页使用 | AI 正确处理分页游标 |
| 错误恢复 | AI 识别并处理 MvidMismatch |

---

## 10. 开发规范

### 10.1 代码规范

- 使用 C# 12 语法
- 强制类型标注
- 异步方法以 `Async` 后缀
- 日志输出到 stdout/stderr

### 10.2 提交规范

```
<type>(<scope>): <subject>

type: feat | fix | docs | refactor | test | chore
scope: core | infra | tools | mcp | tests
```

### 10.3 分支策略

```
main          ← 稳定版本
develop       ← 开发分支
feature/*     ← 功能分支
bugfix/*      ← 修复分支
```

---

## 附录

### A. 错误码

```
1xxx - ID 相关: 1001 MemberNotFound, 1002 MvidMismatch
2xxx - 会话相关: 2001 SessionNotFound, 2002 SessionExpired
3xxx - 验证相关: 3001 ILStackImbalance, 3002 InvalidBranchTarget
4xxx - IO 相关: 4001 AssemblyNotFound, 4002 WritePermissionDenied
```

### B. 参考资料

- [Mono.Cecil Wiki](https://github.com/jbevain/cecil/wiki)
- [MCP 协议规范](https://modelcontextprotocol.io/)
- [ECMA-335 标准](https://www.ecma-international.org/publications-and-standards/standards/ecma-335/)
- [jadx-ai-mcp](https://github.com/xjoker/jadx-ai-mcp)
