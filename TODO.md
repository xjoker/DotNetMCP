# DotNet MCP - TODO 开发清单

> 缺失功能、差异分析和实现优先级  
> 最后更新: 2026-01-19

---

## 1. MCP 工具实现状态总览

### 1.1 统计摘要

| 类别 | 设计数量 | 已实现 | 完成率 |
|------|----------|--------|--------|
| 分析工具 | 9 | 9 | **100%** |
| 修改工具 | 5 | 5 | **100%** |
| 实例管理 | 7 | 0 | **0%** |
| 批量操作 | 3 | 3 | **100%** |
| **总计** | **24** | **17** | **71%** |

### 1.2 详细状态表

#### 分析工具 (Analysis)

| 工具名称 | Python MCP | C# REST API | 后端核心能力 | 状态 |
|----------|:----------:|:-----------:|:------------:|------|
| `get_assembly_info` | ✅ | ✅ `/assembly/info` | ✅ | **完成** |
| `get_type_source` | ✅ | ✅ `/analysis/type/{name}/source` | ✅ DecompilerService | **完成** |
| `get_method_by_name` | ✅ | ✅ `/analysis/type/{name}/method/{name}` | ✅ DecompilerService | **完成** |
| `get_type_info` | ✅ | ✅ `/analysis/type/{name}/info` | ✅ AssemblyContext | **完成** |
| `search_types_by_keyword` | ✅ | ✅ `/analysis/search/types` | ✅ SearchService | **完成** |
| `search_string_literals` | ✅ | ✅ `/analysis/search/strings` | ✅ SearchService | **完成** |
| `get_xrefs_to_type` | ✅ | ✅ `/analysis/xrefs/type/{name}` | ✅ CrossReferenceAnalyzer | **完成** |
| `get_xrefs_to_method` | ✅ | ✅ `/analysis/xrefs/method/{type}/{method}` | ✅ CrossReferenceAnalyzer | **完成** |
| `build_call_graph` | ✅ | ✅ `/analysis/callgraph/{type}/{method}` | ✅ CallGraphAnalyzer | **完成** |

#### 修改工具 (Modification)

| 工具名称 | Python MCP | C# REST API | 后端核心能力 | 状态 |
|----------|:----------:|:-----------:|:------------:|------|
| `inject_method_entry` | ✅ | ✅ `/modification/inject/entry` | ✅ CodeInjector | **完成** |
| `replace_method_body` | ✅ | ✅ `/modification/replace/body` | ✅ ILBuilder | **完成** |
| `add_type` | ✅ | ✅ `/modification/type/add` | ✅ TypeFactory | **完成** |
| `add_method` | ✅ | ✅ `/modification/method/add` | ✅ TypeFactory | **完成** |
| `save_assembly` | ✅ | ✅ `/modification/save` | ✅ AssemblyRewriter | **完成** |

#### 实例管理工具 (Instance)

| 工具名称 | Python MCP | C# REST API | 后端核心能力 | 状态 |
|----------|:----------:|:-----------:|:------------:|------|
| `list_instances` | ✅ | ❌ | 🔸 简单实现 | TODO |
| `add_instance` | ✅ | ❌ | ❌ 未实现 | TODO |
| `remove_instance` | ✅ | ❌ | ❌ 未实现 | TODO |
| `set_default_instance` | ✅ | ❌ | ❌ 未实现 | TODO |
| `get_analysis_status` | ✅ | ❌ | ❌ 未实现 | TODO |
| `clear_cache` | ✅ | ❌ | ❌ 未实现 | TODO |
| `health_check_instances` | ✅ | ❌ | ❌ 未实现 | TODO |

#### 批量工具 (Batch)

| 工具名称 | Python MCP | C# REST API | 后端核心能力 | 状态 |
|----------|:----------:|:-----------:|:------------:|------|
| `batch_get_type_source` | ✅ | ✅ `/analysis/batch/sources` | ✅ | **完成** |
| `batch_get_method_by_name` | ✅ | ✅ `/analysis/batch/methods` | ✅ | **完成** |
| `batch_get_xrefs` | ✅ | ✅ `/analysis/batch/xrefs` | ✅ | **完成** |

---

## 2. 与 jadx-mcp 差距分析

### 2.1 功能对比

| 维度 | jadx-mcp (Java/Android) | DotNet MCP (C#/.NET) | 差距评估 |
|------|------------------------|----------------------|----------|
| **分析工具数** | ~15 完整 | 1/9 完成 | 🔴 落后 ~80% |
| **反编译能力** | ✅ 完整对外暴露 | ✅ 后端有，❌ 无API | 🟡 需接入 |
| **搜索功能** | ✅ 多模式搜索 | ✅ 后端有，❌ 无API | 🟡 需接入 |
| **交叉引用** | ✅ 完整实现 | ✅ 后端有，❌ 无API | 🟡 需接入 |
| **调用图** | ✅ 完整实现 | ✅ 后端有，❌ 无API | 🟡 需接入 |
| **修改能力** | ❌ 只读 | ✅ 完整 5 个工具 | 🟢 **领先** |
| **实例管理** | ✅ 完整 | ❌ 未实现 | 🔴 落后 |
| **批量操作** | ✅ 完整 | ❌ 未实现 | 🔴 落后 |
| **Docker部署** | ✅ 生产级 | 🔸 框架阶段 | 🟡 部分 |
| **测试覆盖** | ✅ 完整E2E | 🔸 单元测试+部分E2E | 🟡 部分 |
| **文档完善度** | ✅ 完整 | 🔸 基础 | 🟡 部分 |

### 2.2 核心差距

**差距类型 A: 后端已有，缺少 REST API 暴露**
- 反编译 (DecompilerService)
- 搜索 (SearchService)
- 交叉引用 (CrossReferenceAnalyzer)
- 调用图 (CallGraphAnalyzer)
- 索引 (TypeIndex, MemberIndex)

**差距类型 B: 完全缺失**
- 实例管理 API
- 批量操作 API
- 状态监控 API
- 缓存管理 API

**差距类型 C: 基础设施**
- Docker 容器化测试
- 完整 E2E 测试
- CI/CD 集成

---

## 3. TODO 任务列表

### 3.1 优先级 P0 - 核心功能补全 (预计 2 天)

#### TODO-001: 创建 AnalysisController
- **描述**: 暴露分析服务的 REST API
- **依赖**: DecompilerService, SearchService, CrossReferenceAnalyzer, CallGraphAnalyzer
- **端点**:
  ```
  GET  /analysis/type/{typeName}/source?language=csharp
  GET  /analysis/type/{typeName}/method/{methodName}?language=csharp
  GET  /analysis/type/{typeName}/info
  GET  /analysis/search/types?keyword=xxx&namespace=xxx&limit=50
  GET  /analysis/search/strings?query=xxx&mode=contains&limit=50
  GET  /analysis/xrefs/type/{typeName}?limit=50
  GET  /analysis/xrefs/method/{memberId}?limit=50
  GET  /analysis/callgraph/{memberId}?direction=callees&max_depth=3
  ```
- **文件**: `backend-service/src/DotNetMcp.Backend/Controllers/AnalysisController.cs`

#### TODO-002: 创建 AnalysisService
- **描述**: 协调分析操作的服务层
- **文件**: `backend-service/src/DotNetMcp.Backend/Services/AnalysisService.cs`

### 3.2 优先级 P1 - 实例管理 (预计 1 天)

#### TODO-003: 创建 InstanceController
- **描述**: 实例管理 REST API
- **端点**:
  ```
  GET  /instances              # 列出所有实例
  POST /instances              # 添加实例
  DELETE /instances/{name}     # 删除实例
  PUT  /instances/{name}/default # 设为默认
  GET  /status                 # 分析状态
  POST /cache/clear            # 清除缓存
  ```
- **文件**: `backend-service/src/DotNetMcp.Backend/Controllers/InstanceController.cs`

### 3.3 优先级 P1 - 批量操作 (预计 0.5 天)

#### TODO-004: 添加批量端点到 AnalysisController
- **描述**: 批量获取源码、方法、交叉引用
- **端点**:
  ```
  POST /analysis/batch/sources   # 批量获取类型源码 (max 20)
  POST /analysis/batch/methods   # 批量获取方法 (max 20)
  POST /analysis/batch/xrefs     # 批量获取引用 (max 10)
  ```

### 3.4 优先级 P2 - Docker 容器化 (预计 1 天)

#### TODO-005: 完善 Dockerfile.backend
- **描述**: 多阶段构建，生产级配置
- **文件**: `docker/Dockerfile.backend`

#### TODO-006: 创建 Dockerfile.mcp-server
- **描述**: Python MCP Server 容器
- **文件**: `docker/Dockerfile.mcp-server`

#### TODO-007: 完善 docker-compose.test.yml
- **描述**: 完整测试编排
- **文件**: `docker/docker-compose.test.yml`

### 3.5 优先级 P2 - 完整测试 (预计 1 天)

#### TODO-008: 容器化端到端测试
- **描述**: 在 Docker 中完整测试所有工具
- **文件**: `tests/e2e/`

#### TODO-009: 边界条件测试
- **描述**: 
  - 空输入
  - 超大输入
  - 无效路径
  - 并发请求
  - 异常恢复
- **文件**: `tests/e2e/test_edge_cases.py`

### 3.6 优先级 P3 - 增强功能 (未来)

#### TODO-010: 会话管理
- **描述**: `begin_modify_session`, `commit_session`, `rollback_session`
- **文件**: `backend-service/src/DotNetMcp.Backend/Services/SessionService.cs`

#### TODO-011: 更多修改工具
- **描述**: `wrap_method`, `add_attribute`, `remove_attribute`, `rename_member`

#### TODO-012: CI/CD 集成
- **描述**: GitHub Actions workflow
- **文件**: `.github/workflows/test.yml`

---

## 4. 实现路线图

```
Week 1 (P0):
├── TODO-001: AnalysisController ────────── 8h
├── TODO-002: AnalysisService ──────────── 4h
└── 验证所有分析工具端到端可用

Week 2 (P1):
├── TODO-003: InstanceController ────────── 4h
├── TODO-004: 批量端点 ─────────────────── 4h
└── 更新 Python MCP 工具适配

Week 3 (P2):
├── TODO-005/006/007: Docker ────────────── 8h
├── TODO-008: 容器化测试 ──────────────── 6h
└── TODO-009: 边界测试 ────────────────── 4h

Week 4 (P3, 可选):
├── TODO-010: 会话管理
├── TODO-011: 增强修改工具
└── TODO-012: CI/CD
```

---

## 5. 快速定位

### 5.1 需要创建的文件

| 文件 | 用途 |
|------|------|
| `Controllers/AnalysisController.cs` | 分析 API |
| `Controllers/InstanceController.cs` | 实例管理 API |
| `Services/AnalysisService.cs` | 分析服务层 |
| `docker/Dockerfile.mcp-server` | Python 容器 |
| `docker/docker-compose.test.yml` | 测试编排 |
| `tests/e2e/test_edge_cases.py` | 边界测试 |

### 5.2 需要修改的文件

| 文件 | 修改内容 |
|------|----------|
| `Program.cs` | 注册新服务/控制器 |
| `mcp-server/src/server/tools/analysis.py` | 适配新 API |
| `mcp-server/src/server/tools/instance.py` | 适配新 API |
| `mcp-server/src/server/tools/batch.py` | 适配新 API |

---

## 6. 验收标准

### 阶段一完成标准 (P0)
- [ ] 所有 9 个分析工具 REST API 可用
- [ ] Python MCP 工具能调用后端 API
- [ ] 通过 curl 手动测试全部端点

### 阶段二完成标准 (P1)
- [ ] 实例管理 7 个端点可用
- [ ] 批量操作 3 个端点可用
- [ ] 单元测试覆盖新增代码

### 阶段三完成标准 (P2)
- [ ] Docker 容器可构建并运行
- [ ] 容器内完整测试通过
- [ ] 边界条件测试覆盖

---

## 更新日志

| 日期 | 变更 |
|------|------|
| 2026-01-19 | 初始版本，整理 24 个工具状态 |
