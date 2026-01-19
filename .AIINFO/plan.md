# DotNet MCP 开发计划

## 执行摘要
实现 AI 静态逆向工程 MCP 服务，采用分离式架构（Python MCP Server + C# 后端）。

## 当前阶段
**Phase 1: Week 1-2 - Cecil 集成与上下文管理（进行中）**

## 阶段计划

### Phase 1: 后端基础设施（7 周）
- [x] Week 1-2: Cecil 集成（进行中）
  - [x] AssemblyContext 实现
  - [x] CustomAssemblyResolver 三级策略
  - [x] REST API 端点
  - [ ] 单元测试编写
- [ ] Week 3: ID 系统
- [ ] Week 4: 分页与切片
- [ ] Week 5-6: Roslyn 编译
- [ ] Week 7: 测试与文档

### Phase 2-4: 待规划

## 风险评估
- ✅ .NET 10 SDK 未发布 → 已降级至 .NET 9
- ⚠️ 依赖解析成功率需验证
