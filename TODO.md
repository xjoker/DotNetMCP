# DotNetMCP TODO

> 最后更新: 2026-01-21 11:45 (Asia/Shanghai)  
> **MCP 工具总数: 26 个** (整合自 53 个，减少 51%)

---

## ✅ 工具整合 (2026-01-21 完成)

| 分类 | 工具 | 功能 |
|------|------|------|
| **Core (4)** | get_assembly_info, get_type_source, get_method_source, get_type_info | 核心分析 |
| **Search (1)** | search | 统一搜索 (type/member/literal/token/regex) |
| **XRefs (1)** | get_xrefs | 交叉引用 (type/method/field + 批量) |
| **Graphs (2)** | build_call_graph, build_cfg | 调用图 + 控制流图 (支配树/数据流) |
| **Detection (1)** | detect | 模式检测 + 混淆检测 |
| **Instance (4)** | list_instances, set_default, remove, clear_cache | 实例管理 |
| **Modification (3)** | inject_code, replace_body, save_assembly | IL修改 |
| **Resources (4)** | list, get, set, remove | 嵌入式资源 |
| **Dependencies (1)** | get_dependencies | 程序集/类型依赖 |
| **Transaction (3)** | begin, commit, rollback | 修改事务 |
| **Transfer (1)** | create_transfer_token | 大文件传输 |
| **Export (1)** | export | 统一导出 (types/namespace/report) |

---

## 🧪 工具测试要求

### Core (4 个)

| 工具 | 测试用例 | 验收标准 |
|------|----------|----------|
| `get_assembly_info` | 加载 DLL → 获取信息 | 返回 name, version, types_count |
| `get_type_source` | 单个类型 / 批量 20 类型 | 返回有效 C# 代码 |
| `get_method_source` | 单个方法 / 批量 20 方法 | 返回方法签名和体 |
| `get_type_info` | 获取带继承的类型 | 返回 base_type, interfaces, methods |

### Search (1 个)

| 工具 | 测试用例 | 验收标准 |
|------|----------|----------|
| `search` | mode=type, 关键词 "User" | 返回匹配类型列表 |
| | mode=member, 关键词 "Get" | 返回匹配方法列表 |
| | mode=literal, 搜索 "Hello" | 返回字符串位置 |
| | 高级语法 "+Button -Test" | 正确过滤 |
| | 正则 "/^On.*$/" | 正则匹配生效 |

### XRefs (1 个)

| 工具 | 测试用例 | 验收标准 |
|------|----------|----------|
| `get_xrefs` | target_type=type | 返回类型引用位置 |
| | target_type=method | 返回方法调用位置 |
| | 批量 10 类型 | 返回多个类型的引用 |

### Graphs (2 个)

| 工具 | 测试用例 | 验收标准 |
|------|----------|----------|
| `build_call_graph` | 基础调用图 | 返回节点和边 |
| | enhanced=True | 包含委托/反射调用 |
| | detect_recursion=True | 检测递归 |
| `build_cfg` | format=json | 返回基本块列表 |
| | format=mermaid | 返回 Mermaid 代码 |
| | include_dominators=True | 包含支配树 |
| | include_dataflow=True | 包含活跃变量 |

### Detection (1 个)

| 工具 | 测试用例 | 验收标准 |
|------|----------|----------|
| `detect` | type=patterns | 返回设计模式列表 |
| | type=obfuscation | 返回混淆评分 |
| | type=all | 同时返回两者 |

### Instance (4 个)

| 工具 | 测试用例 | 验收标准 |
|------|----------|----------|
| `list_instances` | 无参数 | 返回实例列表 |
| | include_health=True | 包含健康状态 |
| `set_default_instance` | 设置有效 MVID | success=True |
| `remove_instance` | 移除已加载实例 | success=True |
| `clear_cache` | 清除缓存 | 返回内存信息 |

### Modification (3 个)

| 工具 | 测试用例 | 验收标准 |
|------|----------|----------|
| `inject_code` | 注入 ldstr+nop | 方法入口被修改 |
| `replace_body` | 替换为 ldc.i4+ret | 方法体被替换 |
| `save_assembly` | 保存到 /tmp | 文件存在且可加载 |

### Resources (4 个)

| 工具 | 测试用例 | 验收标准 |
|------|----------|----------|
| `list_resources` | 无参数 | 返回资源列表 |
| | export_all=True | 包含 base64 内容 |
| `get_resource` | 获取存在的资源 | 返回内容 |
| `set_resource` | 添加新资源 | success=True |
| | 替换已有资源 | 内容更新 |
| `remove_resource` | 删除资源 | success=True |

### Dependencies (1 个)

| 工具 | 测试用例 | 验收标准 |
|------|----------|----------|
| `get_dependencies` | scope=assembly | 返回程序集依赖 |
| | scope=type | 返回类型依赖 |
| | format=mermaid | 返回 Mermaid 代码 |

### Transaction (3 个)

| 工具 | 测试用例 | 验收标准 |
|------|----------|----------|
| `begin_transaction` | 开始事务 | 返回 transaction_id |
| `commit_transaction` | 提交事务 | success=True |
| `rollback_transaction` | 回滚后检查 | 状态恢复 |

### Transfer (1 个)

| 工具 | 测试用例 | 验收标准 |
|------|----------|----------|
| `create_transfer_token` | operation=upload | 返回 token + expires_at |
| | operation=download | 返回有效 token |

### Export (1 个)

| 工具 | 测试用例 | 验收标准 |
|------|----------|----------|
| `export` | scope=types | 返回 ZIP (base64) |
| | scope=namespace | 返回命名空间 ZIP |
| | scope=report | 返回完整分析报告 |

---

## ⏳ 待执行

- [ ] 端到端测试 (基于上述用例)
- [ ] 性能优化
- [ ] README 更新

---

## 📅 历史记录

### 2026-01-21
- [x] 工具整合: 53 → 26 (减少 51%)
- [x] 核心分析模块增强
- [x] 测试要求文档化

### 2026-01-20
- [x] P0-P2 全部完成
