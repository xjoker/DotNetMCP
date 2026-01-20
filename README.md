# DotNet MCP Project

## 项目概述

基于 MCP (Model Context Protocol) 的 .NET 程序集逆向工程和修改工具，提供完整的分析、修改和高级检测能力。

**MCP 工具总数**: 41 个 (分析 15 + 修改 5 + 实例 7 + 批量 6 + 资源 6 + 传输 3 + 导出 3)

## 架构

```mermaid
graph TB
    subgraph "AI Client"
        A["Claude / Cursor"]
    end
    
    subgraph "MCP Server"
        B["Python MCP Server<br/>(FastMCP)"]
    end
    
    subgraph "Backend"
        C["C# Backend Service<br/>(ASP.NET Core + Mono.Cecil)"]
    end
    
    A -->|"MCP Protocol<br/>(HTTP/stdio)"| B
    B -->|"REST API<br/>(HTTP)"| C
```


## 功能模块

### Phase 1: 基础设施 ✅
- **Cecil 集成**: 程序集加载和上下文管理
- **ID 系统**: MemberId, LocationId 编解码
- **分页系统**: Cursor-based 分页和切片
- **Roslyn 编译**: C# 代码运行时编译

### Phase 2: 分析能力 ✅
- **索引服务**: TypeIndex, MemberIndex
- **搜索服务**: 统一搜索接口
- **反编译**: 基于 ILSpy 的 C# 反编译
- **交叉引用**: 类型和方法引用查找
- **调用图**: 方法调用图构建

### Phase 3: 修改能力 ✅
- **ILBuilder**: IL 指令序列构建器
- **CodeInjector**: 代码注入器
- **AssemblyRewriter**: 程序集重写器
- **TypeFactory**: 类型工厂
- **DiffComparator**: 差异对比器

## ✨ 高级分析功能

### 依赖图分析
- **程序集级依赖**: 分析程序集引用关系
- **类型级依赖**: 分析继承、接口、字段、方法参数等依赖
- **Mermaid 可视化**: 支持 Mermaid 格式图输出

### 控制流图 (CFG)
- **基本块识别**: Leaders 算法识别基本块
- **分支分析**: 支持 br、brfalse、brtrue、switch 指令
- **异常处理**: 分析 try-catch-finally 边界
- **Mermaid 可视化**: 可视化方法执行流程

### 设计模式检测
自动识别常见设计模式：
- **创建型**: Singleton, Factory, Abstract Factory, Builder
- **结构型**: Adapter
- **行为型**: Observer

特性：
- 置信度评分 (0.0-1.0)
- 详细证据列表
- 可配置最小置信度阈值

### 混淆检测
识别代码混淆技术：
- **标识符混淆**: 检测类型名/方法名/字段名混淆
- **控制流平坦化**: 识别 switch-based 状态机
- **字符串加密**: 检测字符串解密调用
- **垃圾代码**: 识别 NOP 指令和无意义分支

特性：
- 混淆评分 (0.0-1.0)
- 严重程度分级 (Low/Medium/High)
- 详细检测证据

### 批量导出
- **类型导出**: 批量导出多个类型源码到 ZIP
- **命名空间导出**: 导出完整命名空间
- **分析报告**: 导出包含源码、依赖图、模式检测、混淆分析的完整报告

### Phase 4: MCP 集成 ✅
- **Python MCP Server**: FastMCP 框架
- **工具注册**: 分析和修改工具
- **REST API 适配**: Python ↔ C# 对接

## 快速开始

### 启动后端服务

```bash
cd backend-service
dotnet run --project src/DotNetMcp.Backend
```

服务将在 `http://localhost:5000` 启动。

### 启动 MCP Server

```bash
cd mcp-server
python dotnetmcp_server.py
```

### 加载程序集

```bash
curl -X POST http://localhost:5000/assembly/load \
  -H "Content-Type: application/json" \
  -d '{"path": "/path/to/assembly.dll"}'
```

### 注入代码示例

```bash
curl -X POST http://localhost:5000/modification/inject/entry \
  -H "Content-Type: application/json" \
  -d '{
    "methodFullName": "MyApp.Program::Main",
    "instructions": [
      {"opCode": "ldstr", "stringValue": "Hello from injected code!"},
      {"opCode": "call", "stringValue": "System.Console::WriteLine"}
    ]
  }'
```

### 保存修改后的程序集

```bash
curl -X POST http://localhost:5000/modification/save \
  -H "Content-Type: application/json" \
  -d '{"outputPath": "/tmp/modified.dll"}'
```

## REST API 端点

### Assembly Management
- `POST /assembly/load` - 加载程序集
- `GET /assembly/info` - 获取程序集信息
- `GET /health` - 健康检查

### Modification
- `POST /modification/inject/entry` - 注入方法入口代码
- `POST /modification/replace/body` - 替换方法体
- `POST /modification/type/add` - 添加新类型
- `POST /modification/method/add` - 添加方法
- `POST /modification/save` - 保存程序集

## MCP 工具

### Analysis Tools
- `get_assembly_info` - 获取程序集信息
- `get_type_source` - 获取类型源码
- `search_types_by_keyword` - 搜索类型

### Modification Tools
- `inject_method_entry` - 注入方法入口
- `replace_method_body` - 替换方法体
- `add_type` - 添加类型
- `add_method` - 添加方法
- `save_assembly` - 保存程序集

## 测试

```bash
cd backend-service
dotnet test
```

当前测试状态: **113 个测试全部通过** ✅

## 依赖

### C# Backend
- Mono.Cecil - 程序集操作
- ILSpy (ICSharpCode.Decompiler) - 反编译
- Microsoft.CodeAnalysis (Roslyn) - C# 编译

### Python MCP Server
- fastmcp - MCP 框架
- httpx - HTTP 客户端

## 开发状态

| Phase | 状态 | 测试 |
|-------|------|------|
| Phase 1: 基础设施 | ✅ | 74 个 |
| Phase 2: 分析能力 | ✅ | 19 个 |
| Phase 3: 修改能力 | ✅ | 20 个 |
| Phase 4: MCP 集成 | 🚧 | - |

**总测试数**: 113 个 ✅

## License

MIT
