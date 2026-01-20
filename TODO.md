# DotNetMCP TODO

> 最后更新: 2026-01-20 13:37 (Asia/Shanghai)  
> **MCP 工具总数: 36 个** (分析 12 + 修改 5 + 实例 7 + 批量 3 + 资源 6 + 传输 3)

---

## ✅ P0 - 技术债务 (已完成)

| # | 任务 | 描述 | 状态 |
|:-:|------|------|:----:|
| 1 | 统一实例管理 | 创建 `InstanceRegistry` 单例服务，替换各 Controller 分散的 `_contexts` 字典 | ✅ |
| 2 | 修复 inject/replace | 增强 `FindMethod` 诊断信息，找不到方法时显示可用方法 | ✅ |
| 3 | 修复 export_all 路由 | `GET /resources/export` 被误匹配为资源名，改为 `POST /resources/export-all` | ✅ |

---

## ✅ P1 - 功能增强 (已完成)

| # | 任务 | 描述 | 状态 |
|:-:|------|------|:----:|
| 4 | 依赖图分析 | 程序集/类型级依赖关系，输出 Mermaid 格式 | ✅ |
| 5 | 控制流图 (CFG) | 方法内基本块识别、分支指令分析、Mermaid 输出 | ✅ |
| 6 | E2E 测试 | `docker-compose-e2e.yml` 配置完成，MCP 链路测试 89% 通过 | ✅ |

---

## ✅ P2 - 高级分析 (已完成)

| # | 任务 | 描述 | 状态 |
|:-:|------|------|:----:|
| 7 | 设计模式检测 | 单例/工厂/观察者/建造者/适配器模式自动识别 | ✅ |
| 8 | 混淆检测 | 非法标识符、控制流平坦化、字符串加密特征检测 | ✅ |
| 9 | 批量代码下载 | ZIP 打包下载多个类型/方法源码、完整分析报告 | ✅ |

---

## ✅ P3 - 可选增强 (已完成 4/5)

| # | 任务 | 描述 | 状态 |
|:-:|------|------|:----:|
| 10 | 会话事务 | begin/commit/rollback 修改事务 | ✅ |
| 11 | 签名管理 | get_signature, remove_signature, resign | ✅ |
| 12 | 传输 API 增强 | 速率限制、文件类型白名单 | ✅ |
| 13 | CI/CD 集成 | GitHub Actions 自动构建/测试 | ✅ |
| 14 | 性能优化 | 大程序集并发分析、内存优化 | ⏳ |

---

## ✅ 已完成

### 2026-01-20
- [x] **P0 技术债务** (3/3): 统一实例管理、FindMethod 诊断、export_all 路由
- [x] **P1 功能增强** (3/3): 依赖图分析、控制流图 (CFG)、E2E 测试
- [x] 新增 MCP 工具 (3个): get_assembly_dependencies, get_type_dependencies, build_control_flow_graph
- [x] 代码重构和优化: 净增 1031 行 (删除 329 + 新增 1360)

### 2026-01-19
- [x] Token 认证中间件 (`ApiKeyAuthMiddleware.cs`)
- [x] 多用户隔离验证
- [x] 边界验证修复 (空值、负数、无效参数)
- [x] 方法签名多格式支持 (`.` 和 `::`)
- [x] 自动依赖加载功能
- [x] 资源管理工具 (6个): list/add/get/replace/remove/export
- [x] 传输工具 (3个): create_token/status/revoke
- [x] 大文件 API: upload/download
- [x] .NET 10.0 升级
- [x] Docker 配置更新 (multiuser, e2e, seq-test)

### 2026-01-18
- [x] 33 个 MCP 工具实现
- [x] 完整 MCP 链路测试 (89% 通过)
