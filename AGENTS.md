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

## 开发约定

### 项目结构
- `src/DotNetMcp/Core/` - 核心服务实现
- `src/DotNetMcp/Infrastructure/` - 基础设施组件
- `src/DotNetMcp/Tools/` - MCP 工具
- `src/DotNetMcp/Mcp/` - MCP 协议实现

### 命名规范
- 类名：PascalCase
- 方法名：PascalCase
- 私有字段：_camelCase

### 依赖
- Mono.Cecil >= 0.11.5
- ICSharpCode.Decompiler >= 8.0

## 关键设计决策

1. 使用 IL 偏移定位，不依赖 PDB
2. 修改后生成 ID 映射表（基于签名匹配）
3. Cecil Immediate 模式（支持修改）
4. 四层验证体系
