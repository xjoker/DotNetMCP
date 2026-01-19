# Context - DotNetMCP 项目

**最后更新**: 2026-01-19 上午 10:45 (Asia/Shanghai)

## 当前会话状态

### 进行中
- Phase 3 修改能力实现已完成核心组件

### 已完成
- **Phase 1 (7周)**: 后端基础设施 - Cecil集成、ID系统、分页切片、Roslyn编译
- **Phase 2 (6周)**: 分析能力 - 索引构建、搜索服务、反编译、交叉引用、调用图
- **Phase 3**: 修改能力核心组件
  - `ILBuilder` - IL指令构建器
  - `CodeInjector` - 代码注入器  
  - `AssemblyRewriter` - 程序集重写器
  - `TypeFactory` - 类型工厂
  - `DiffComparator` - 差异对比器

### 测试状态
- 总计 113 个单元测试全部通过
- Phase 3 新增 20 个测试

## 项目结构
```
/Volumes/2TB_Disk/SourceCode/IL_mcp/
├── backend-service/
│   ├── src/DotNetMcp.Backend/Core/
│   │   ├── Context/      # 程序集上下文
│   │   ├── Identity/     # ID系统
│   │   ├── Paging/       # 分页切片
│   │   ├── Compilation/  # Roslyn编译
│   │   ├── Analysis/     # 分析服务
│   │   └── Modification/ # 修改服务 ★
│   └── tests/            # 单元测试
└── mcp-server/           # Python MCP服务器
```

## 关键依赖
- Mono.Cecil - 程序集操作
- ILSpy (ICSharpCode.Decompiler) - 反编译
- Microsoft.CodeAnalysis (Roslyn) - C#编译
