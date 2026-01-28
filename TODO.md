# DotNetMCP TODO

> 最后更新: 2026-01-28 (Asia/Shanghai)

## 🔴 优先修复

| 问题 | 描述 | 状态 |
|------|------|------|
| ~~统一默认实例管理~~ | ~~各控制器有独立的默认实例存储~~ | ✅ 已完成 |

---

## 🟡 高级分析工具 (P0-P2)

### Phase 1: 控制流图 (CFG) [P0] ✅
- [x] 创建 `Core/Analysis/ControlFlowGraphBuilder.cs`
  - [x] 基本块 (Basic Block) 识别
  - [x] 分支指令分析 (br, brfalse, brtrue, switch)
  - [x] 节点/边结构定义
  - [x] Mermaid 格式输出
- [x] 添加 `AnalysisService.BuildControlFlowGraph()` 方法
- [x] 添加 API 端点 `GET /analysis/cfg/{type}/{method}`
- [ ] MCP 工具集成 `build_control_flow_graph`

### Phase 2: 依赖图 [P0] ✅
- [x] 创建 `Core/Analysis/DependencyGraphBuilder.cs`
  - [x] 程序集级依赖分析
  - [x] 类型级依赖分析
  - [x] 可视化输出 (Mermaid)
- [x] 添加 API 端点 `GET /analysis/dependencies`
- [ ] MCP 工具集成 `build_dependency_graph`

### Phase 3: 设计模式检测 [P1] ✅
- [x] 创建 `Core/Analysis/PatternDetector.cs`
  - [x] 单例模式检测 (私有构造函数 + 静态实例)
  - [x] 工厂模式检测 (Create/Build/Get 方法)
  - [x] 观察者模式检测 (EventHandler + add/remove)
- [x] 添加 API 端点 `GET /analysis/patterns`
- [ ] MCP 工具集成 `detect_design_patterns`

### Phase 4: 混淆检测 [P2]
- [ ] 创建 `Core/Analysis/ObfuscationDetector.cs`
  - [ ] 非法标识符名称检测
  - [ ] 随机/超短类型名检测
  - [ ] 控制流平坦化特征检测
- [ ] 添加 API 端点 `GET /analysis/obfuscation`

---

## ✅ 已完成 (2026-01-19)

- [x] Token 认证中间件 (`ApiKeyAuthMiddleware.cs`)
- [x] 多用户隔离验证
- [x] 边界验证修复 (空值、负数、无效参数)
- [x] 方法签名多格式支持 (`.` 和 `::`)
- [x] 自动依赖加载功能
