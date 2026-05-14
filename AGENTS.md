# DotNet MCP 项目 AI 开发指南

## 项目概述

AI 静态逆向工程 MCP 服务，使 AI 能够自主进行 .NET 程序集的静态分析与修改。

## 核心概念

### 稳定标识符系统

- **MemberId**: `<mvid>:<token>:<kind>` - 成员标识
- **LocationId**: `<memberId>@<ilOffset>` - 位置标识

### 分层架构

1. MCP 协议层 → 工具路由
2. 能力层 → 分析/修改工具
3. 核心服务层 → 索引/反编译/修改/验证
4. 基础设施层 → ID/分页/切片/事务

### 新增模块（Wave 6+）

- **Roslyn Patch 模块**（`ModificationTools.cs`）：`replace_method_body_with_csharp` 通过 Roslyn 编译 C# 片段，再由 Cecil 将生成的 IL 合并回目标方法，无需手写 IL 操作码。
- **Workspace Alias 模块**（`InstanceTools.cs`）：`register_assembly_alias` / `unregister_assembly_alias` / `list_assembly_aliases` / `instance_restore_persisted` 提供 MVID 短名映射，alias 持久化到 LocalAppData/`dotnet-mcp/aliases.json`，支持跨会话恢复。
- **Lazy 索引 / 预热**（`AnalysisTools.cs`）：搜索和分析类工具自动使用缓存索引；`warm_index` 提供显式预热入口，支持软超时（`maxSeconds`）。

## 开发约定

### 项目结构
- `src/DotNetMcp.Server/` - MCP 服务端（工具、配置、后端管理）
- `src/DotNetMcp.Backend/` - 核心后端（分析、反编译、修改服务）
- `tests/DotNetMcp.Server.Tests/` - Server 单元测试
- `tests/DotNetMcp.Backend.Tests/` - Backend 单元测试

### 命名规范
- 类名：PascalCase
- 方法名：PascalCase
- 私有字段：_camelCase

### 依赖
- Mono.Cecil 0.11.6
- ICSharpCode.Decompiler 10.0.0.8330
- Microsoft.CodeAnalysis.CSharp 5.3.0
- ModelContextProtocol 1.2.0

## 关键设计决策

1. 使用 IL 偏移定位，不依赖 PDB
2. 修改后生成 ID 映射表（基于签名匹配）
3. Cecil Immediate 模式（支持修改）
4. 四层验证体系
