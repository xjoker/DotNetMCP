# 工具参考

[English](../en/tools-reference.md) | 中文

本文档详细介绍所有 MCP 工具的参数和使用方法，包括 AI 对话示例。

---

## 程序集管理工具

### load_assembly

加载 .NET 程序集进行分析。

**使用场景：**
- 开始分析一个新的 DLL/EXE 文件
- 需要分析程序集内部实现

**AI 对话示例：**
> "加载 /path/to/MyApp.dll"
>
> "帮我分析这个程序集：C:\Projects\MyLib.dll"
>
> "打开程序集 ./bin/Debug/net8.0/App.dll 并告诉我有什么类型"
>
> "加载 MyApp.dll，依赖目录为 /path/to/libs"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `path` | string | 是 | 程序集文件路径（.dll 或 .exe） |
| `searchPaths` | string[] | 否 | 依赖项搜索目录，用于解析引用 |
| `backendId` | string | 否 | 指定后端 ID |

**返回示例：**
```json
{
  "success": true,
  "mvid": "12345678-1234-1234-1234-123456789abc",
  "name": "MyAssembly",
  "backend": "local"
}
```

**注意事项：**
- 路径需要是绝对路径或相对于当前工作目录
- 如果程序集有外部依赖，使用 searchPaths 指定依赖目录

---

### list_assemblies

列出已加载的程序集。

**使用场景：**
- 查看当前会话中已加载的所有程序集
- 获取程序集 MVID 用于后续操作

**AI 对话示例：**
> "列出已加载的程序集"
>
> "有哪些程序集已经加载了？"
>
> "显示所有加载的 DLL"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `backendId` | string | 否 | 目标后端 ID |

**返回示例：**
```json
{
  "success": true,
  "assemblies": [
    {
      "mvid": "12345678-...",
      "name": "MyAssembly",
      "path": "/path/to/assembly.dll",
      "isDefault": true
    }
  ]
}
```

---

### unload_assembly

卸载程序集。

**使用场景：**
- 释放不再需要的程序集
- 清理会话资源

**AI 对话示例：**
> "卸载 MyApp 程序集"
>
> "不再需要分析 MyLib.dll 了，卸载它"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `mvid` | string | 是 | 程序集 MVID |
| `backendId` | string | 否 | 目标后端 ID |

---

## 搜索工具

### search_types

按关键词搜索类型。

**使用场景：**
- 在程序集中查找特定类型
- 探索程序集结构
- 按命名空间筛选类型

**AI 对话示例：**
> "搜索名称包含 Service 的类型"
>
> "找出所有 Controller 类"
>
> "列出 MyApp.Services 命名空间下的所有类型"
>
> "有哪些类型？"（使用空关键词列出所有）

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `keyword` | string | 是 | 搜索关键词（空字符串匹配所有） |
| `namespaceFilter` | string | 否 | 命名空间过滤 |
| `limit` | int | 否 | 结果数量限制（默认 50） |
| `mvid` | string | 否 | 指定程序集 MVID |

**返回示例：**
```json
{
  "success": true,
  "types": [
    {
      "fullName": "MyNamespace.MyClass",
      "namespace": "MyNamespace",
      "name": "MyClass",
      "kind": "class",
      "methodCount": 5,
      "fieldCount": 2
    }
  ],
  "totalCount": 1
}
```

---

### search_strings

搜索字符串字面量。

**使用场景：**
- 查找硬编码的密码、密钥
- 搜索 URL、配置字符串
- 分析程序中的文本内容

**AI 对话示例：**
> "搜索包含 password 的字符串"
>
> "找出所有 URL 字符串"
>
> "这个程序集里有没有硬编码的 API key？"
>
> "搜索 http:// 开头的字符串"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `query` | string | 是 | 搜索查询 |
| `mode` | string | 否 | 搜索模式：contains、exact、startswith（默认 contains） |
| `limit` | int | 否 | 结果数量限制（默认 50） |
| `mvid` | string | 否 | 指定程序集 MVID |

**返回示例：**
```json
{
  "success": true,
  "strings": [
    {
      "value": "Invalid password",
      "location": "AuthService.Login",
      "offset": 42
    }
  ],
  "totalCount": 1
}
```

---

## 分析工具

### decompile_type

反编译类型为 C# 或 IL。

**使用场景：**
- 查看类型的完整实现
- 分析类结构和方法
- 理解代码逻辑

**AI 对话示例：**
> "反编译 MyApp.Services.UserService 类"
>
> "看看 UserService 的源码"
>
> "反编译 Program 类为 IL 代码"
>
> "显示 MyClass 的实现"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `typeName` | string | 是 | 完整类型名 |
| `language` | string | 否 | 输出语言：csharp、il（默认 csharp） |
| `preferOriginalSource` | bool | 否 | 优先使用 PDB 中的原始源码 |
| `mvid` | string | 否 | 指定程序集 MVID |

**返回示例：**
```json
{
  "success": true,
  "typeName": "MyNamespace.MyClass",
  "code": "public class MyClass { ... }"
}
```

---

### decompile_method

反编译方法。

**使用场景：**
- 查看特定方法的实现
- 分析方法逻辑
- 查看 IL 指令

**AI 对话示例：**
> "反编译 UserService.GetUser 方法"
>
> "让我看看 Login 方法的代码"
>
> "显示 Main 方法的 IL 代码"
>
> "DoWork 方法是怎么实现的？"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `typeName` | string | 是 | 完整类型名 |
| `methodName` | string | 是 | 方法名 |
| `language` | string | 否 | 输出语言：csharp、il（默认 csharp） |
| `mvid` | string | 否 | 指定程序集 MVID |

**返回示例：**
```json
{
  "success": true,
  "methodName": "GetUser",
  "code": "public User GetUser(int id) { ... }"
}
```

---

### find_type_references

查找类型引用。

**使用场景：**
- 了解类型在哪些地方被使用
- 分析依赖关系
- 评估修改影响范围

**AI 对话示例：**
> "找出哪些地方用到了 UserService 类"
>
> "谁引用了 ILogger 接口？"
>
> "User 类型在哪里被使用？"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `typeName` | string | 是 | 完整类型名 |
| `limit` | int | 否 | 结果数量限制（默认 50） |
| `mvid` | string | 否 | 指定程序集 MVID |

**返回示例：**
```json
{
  "success": true,
  "references": [
    {
      "sourceTypeName": "OtherClass",
      "sourceMemberName": "Method1",
      "targetName": "MyClass",
      "kind": "TypeReference"
    }
  ],
  "totalCount": 1
}
```

---

### find_method_calls

查找方法调用。

**使用场景：**
- 找出哪些地方调用了某个方法
- 分析方法的使用情况
- 追踪代码执行路径

**AI 对话示例：**
> "谁调用了 ValidateToken 方法？"
>
> "找出所有调用 SaveUser 的地方"
>
> "GetData 方法被哪里使用？"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `typeName` | string | 是 | 完整类型名 |
| `methodName` | string | 是 | 方法名 |
| `limit` | int | 否 | 结果数量限制（默认 50） |
| `mvid` | string | 否 | 指定程序集 MVID |

**返回示例：**
```json
{
  "success": true,
  "calls": [
    {
      "callerType": "OrderService",
      "callerMethod": "ProcessOrder",
      "offset": 24
    }
  ],
  "totalCount": 1
}
```

---

### get_call_graph

构建调用图。

**使用场景：**
- 分析方法的调用链
- 理解代码执行流程
- 可视化方法依赖关系

**AI 对话示例：**
> "分析 Main 方法的调用图"
>
> "ProcessOrder 都调用了哪些方法？"
>
> "显示 Initialize 方法的调用链，深度为 5"
>
> "谁调用了 Login 方法？"（callers 方向）

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `typeName` | string | 是 | 完整类型名 |
| `methodName` | string | 是 | 方法名 |
| `direction` | string | 否 | 方向：callees（被调用者）、callers（调用者）（默认 callees） |
| `maxDepth` | int | 否 | 最大深度（默认 3） |
| `maxNodes` | int | 否 | 最大节点数（默认 100） |
| `mvid` | string | 否 | 指定程序集 MVID |

**返回示例：**
```json
{
  "success": true,
  "startMethod": "MyClass.EntryPoint",
  "levels": [
    { "depth": 1, "methods": ["Method1", "Method2"] },
    { "depth": 2, "methods": ["Method3"] }
  ],
  "maxDepthReached": false
}
```

---

### get_control_flow_graph

构建控制流图。

**使用场景：**
- 分析方法的执行路径
- 理解条件分支和循环
- 可视化复杂方法结构

**AI 对话示例：**
> "显示 ProcessOrder 方法的控制流图"
>
> "分析 ValidateInput 方法的执行路径"
>
> "生成 ComplexMethod 的 CFG，包含 IL 指令"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `typeName` | string | 是 | 完整类型名 |
| `methodName` | string | 是 | 方法名 |
| `includeIL` | bool | 否 | 是否包含 IL 指令（默认 false） |
| `mvid` | string | 否 | 指定程序集 MVID |

**返回示例：**
```json
{
  "success": true,
  "methodName": "ComplexMethod",
  "blockCount": 5,
  "edgeCount": 6,
  "mermaid": "graph TD\n  BB0 --> BB1\n  ..."
}
```

---

### get_type_outline

获取类型元数据大纲（无需反编译）。

**使用场景：**
- 快速了解类型结构
- 列出所有成员而不读取完整源码
- 比 decompile_type 更快

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `typeName` | string | 是 | 完整类型名 |
| `mvid` | string | 否 | 指定程序集 MVID |

---

### plan_chunking

规划类型或方法源码的分块方案。

**使用场景：**
- 将大型源码拆分为 LLM 友好的块
- 规划大类的分页阅读

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `typeName` | string | 是 | 完整类型名 |
| `methodName` | string | 否 | 方法名（仅对该方法分块） |
| `targetChunkSize` | int | 否 | 每块目标字符数（默认 6000） |
| `overlap` | int | 否 | 块间重叠行数（默认 2） |
| `mvid` | string | 否 | 指定程序集 MVID |

---

### compare_assemblies

对比两个已加载的程序集，查找结构差异。

**使用场景：**
- 对比同一程序集的两个版本
- 查找构建间的变更
- 追踪修改后的差异

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `leftMvid` | string | 是 | 原始程序集的 MVID |
| `rightMvid` | string | 是 | 修改后程序集的 MVID |
| `namespaceFilter` | string | 否 | 按命名空间前缀过滤 |
| `includeUnchanged` | bool | 否 | 包含未变更的类型（默认 false） |

---

### batch_decompile

一次调用批量反编译多个类型或方法，带字符预算控制。

**使用场景：**
- 同时反编译多个相关类
- 高效批量分析
- 减少 MCP 往返次数

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `memberKeys` | string[] | 是 | 成员键数组（TypeName 或 TypeName::MethodName） |
| `maxTotalChars` | int | 否 | 最大总字符数（默认 200000） |
| `mvid` | string | 否 | 指定程序集 MVID |

---

### get_dependency_graph

构建程序集依赖图，支持程序集、命名空间、类型三档粒度，返回节点/边统计和 Mermaid 可视化字符串。

**使用场景：**
- 了解程序集引用了哪些外部程序集
- 分析命名空间之间的耦合关系
- 可视化某个类型的继承/引用关系树

**AI 对话示例：**
> "显示这个程序集的依赖图"
>
> "用 Mermaid 展示 MyApp.Services.UserService 的类型依赖关系"
>
> "分析命名空间间的耦合"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `level` | string | 否 | 粒度：assembly（默认）、namespace、type |
| `rootType` | string | 否 | level=type 时必填，根类型完整名（如 `MyNamespace.MyClass`） |
| `maxDepth` | int | 否 | level=type 时的最大遍历深度（默认 3，最大 10） |
| `mvid` | string | 否 | 指定程序集 MVID |
| `backendId` | string | 否 | 指定后端 ID |

**返回字段：**
- `level` - 所用粒度
- `rootId` - 根节点 ID
- `totalNodes` / `externalNodes` / `totalEdges` - 图统计
- `mermaid` - Mermaid 图表字符串

---

### detect_design_patterns

检测程序集中的设计模式，支持 Singleton、Factory、AbstractFactory、Observer、Builder、Strategy、Decorator。

**使用场景：**
- 快速了解代码架构风格
- 逆向时识别已知模式，辅助理解代码意图

**AI 对话示例：**
> "这个程序集用了哪些设计模式？"
>
> "检测 UserService 是否实现了 Singleton 模式"
>
> "扫描整个程序集的设计模式"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `typeName` | string | 否 | 指定类型名；不填则扫描整个程序集 |
| `mvid` | string | 否 | 指定程序集 MVID |
| `backendId` | string | 否 | 指定后端 ID |

**返回字段：**
- `totalCount` - 检测到的模式总数
- `summary` - 文本摘要
- `patterns[]` - 每条结果含 patternType、typeName、confidence、evidence[]、relatedTypes[]

---

### enhanced_search

完整搜索引擎暴露，支持正则、+include/-exclude 过滤、精确匹配、模糊匹配、Metadata Token 查找、字面量自动检测。

**使用场景：**
- 复杂搜索条件（多词 +/-、正则）
- 按 Metadata Token 精确定位
- 跨类型/成员/字面量统一搜索

**AI 对话示例：**
> "搜索名称包含 Auth 但不含 Test 的类型：+Auth -Test"
>
> "用正则搜索所有 Get 开头的方法：/^Get/"
>
> "搜索字面量字符串 'https://api.example.com'"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `query` | string | 是 | 查询字符串，支持高级语法（见说明） |
| `mvid` | string | 否 | 指定程序集 MVID |
| `mode` | string | 否 | 搜索模式：auto（默认）、type、member、method、field、property、event、literal、token |
| `namespaceFilter` | string | 否 | 按命名空间前缀过滤 |
| `limit` | int | 否 | 最大结果数（默认 100，上限 1000） |
| `backendId` | string | 否 | 指定后端 ID |

**查询语法说明：**
- `keyword` — 普通关键词（大小写不敏感）
- `+include -exclude` — 包含/排除过滤
- `=exact` — 精确匹配
- `~fuzzy` — 模糊匹配
- `/regex/` — 正则表达式
- `0xToken` — Metadata Token 查找

**返回字段：**
- `items[]` - 结果列表，每项含 id、name、fullName、kind、declaringType、namespace、value、relevance
- `totalCount` / `hasMore` / `durationMs` / `mode`

---

### find_base_types

查找类型的完整基类链和接口列表。

**使用场景：**
- 了解类型继承树
- 分析类型实现了哪些接口
- 识别外部依赖的基础类型

**AI 对话示例：**
> "UserService 的基类链是什么？"
>
> "MyClass 实现了哪些接口？"
>
> "查找 OrderProcessor 的所有基类"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `typeName` | string | 是 | 完整类型名（如 `MyNamespace.MyClass`） |
| `includeInterfaces` | bool | 否 | 是否包含接口（默认 true） |
| `mvid` | string | 否 | 指定程序集 MVID |
| `backendId` | string | 否 | 指定后端 ID |

**返回字段：**
- `types[]` - 每项含 id、fullName、namespace、kind、isExternal（外部程序集类型）
- `totalCount`

---

### find_derived_types

查找所有继承自指定类型（或实现指定接口）的派生类型。

**使用场景：**
- 找出所有子类
- 分析多态实现
- 找接口的所有间接实现

**AI 对话示例：**
> "哪些类继承了 BaseController？"
>
> "IRepository 接口有哪些实现（包括间接实现）？"
>
> "只给我 Animal 的直接子类"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `typeName` | string | 是 | 基类型或接口完整名 |
| `directOnly` | bool | 否 | 仅返回直接子类（默认 false = 递归全部） |
| `mvid` | string | 否 | 指定程序集 MVID |
| `backendId` | string | 否 | 指定后端 ID |

**返回字段：**
- `types[]` - 含 id、fullName、namespace、kind、isExternal
- `totalCount`

---

### get_implementations

查找直接实现指定接口的所有类型。

**使用场景：**
- 快速找到接口的直接实现类
- 与 find_derived_types 配合覆盖间接实现

**AI 对话示例：**
> "IUserRepository 有哪些实现？"
>
> "找出所有直接实现 IService 接口的类"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `interfaceTypeName` | string | 是 | 接口完整名（如 `MyNamespace.IService`） |
| `mvid` | string | 否 | 指定程序集 MVID |
| `backendId` | string | 否 | 指定后端 ID |

**返回字段：**
- `types[]` - 含 id、fullName、namespace、kind、isExternal
- `totalCount`

---

### get_overrides

查找虚方法或抽象方法在所有派生类型中的覆盖实现。

**使用场景：**
- 分析虚方法的所有入口点
- 找出多态分发的所有目标

**AI 对话示例：**
> "Execute 方法有哪些覆盖实现？"
>
> "BaseHandler.Handle 方法在派生类里都有哪些实现？"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `typeName` | string | 是 | 声明类型完整名 |
| `methodName` | string | 是 | 虚方法或抽象方法名 |
| `mvid` | string | 否 | 指定程序集 MVID |
| `backendId` | string | 否 | 指定后端 ID |

**返回字段：**
- `methods[]` - 每项含 id、typeFullName、methodName、signature
- `totalCount`

---

### get_overloads

查找同一类型中指定方法名的所有重载。

**使用场景：**
- 方法名模糊时确认正确签名
- 调用 decompile_method 前确认重载版本

**AI 对话示例：**
> "Parse 方法有哪些重载？"
>
> "列出 UserService.GetUser 的所有重载"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `typeName` | string | 是 | 类型完整名 |
| `methodName` | string | 是 | 方法名 |
| `mvid` | string | 否 | 指定程序集 MVID |
| `backendId` | string | 否 | 指定后端 ID |

**返回字段：**
- `methods[]` - 每项含 id、typeFullName、methodName、signature
- `totalCount`

---

### detect_obfuscation

检测程序集是否被混淆，识别混淆器，返回 0-100 评分、置信度及混淆指标。

**使用场景：**
- 逆向前判断是否需要先去混淆
- 识别混淆器类型以选择合适的反混淆工具

**AI 对话示例：**
> "这个程序集被混淆了吗？"
>
> "检测混淆情况，告诉我用了什么混淆器"
>
> "分析程序集的混淆程度"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `mvid` | string | 否 | 指定程序集 MVID |
| `backendId` | string | 否 | 指定后端 ID |

**返回字段：**
- `isObfuscated` - 是否被混淆
- `obfuscationScore` - 混淆评分（0-100）
- `confidence` - 置信度（Low/Medium/High）
- `detectedObfuscators[]` - 识别到的混淆器名称
- `topIndicators[]` - Top 10 指标，每项含 category、severity、description、location
- `stats` - 统计数据（类型数、方法数、短名称数、无效名称数、控制流平坦化数、代理方法数等）

---

### warm_index

预构建类型和成员索引，加速后续查询。

**使用场景：**
- 对大型程序集进行重度分析前提前预热索引
- 减少批量分析场景中第一次查询的延迟
- 索引默认在首次访问时按需构建，此工具提供显式预热入口

**AI 对话示例：**
> "在开始分析之前先预热这个程序集的索引"
>
> "把类型索引现在构建好，这样查询会更快"
>
> "用 30 秒时间预热索引"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `mvid` | string | 否 | 程序集 MVID 或 alias，省略则使用默认程序集 |
| `typeIndex` | bool | 否 | 是否构建类型索引（默认 true） |
| `memberIndex` | bool | 否 | 是否构建成员索引（默认 true） |
| `maxSeconds` | int | 否 | 软超时秒数，超出后跳过成员索引构建 |
| `backendId` | string | 否 | 指定后端 ID |

**返回字段：**
- `typeIndexBuilt` - 类型索引是否已构建
- `memberIndexBuilt` - 成员索引是否已构建
- `typeCount` - 已索引的类型数量
- `memberCount` - 已索引的成员数量
- `elapsedMs` - 耗时（毫秒）
- `maxSecondsExceeded` - 是否超出了软超时限制

---

## 程序集管理工具（扩展）

### detect_unity_assembly

探测 Unity 游戏目录中的 Assembly-CSharp.dll，支持 Windows/macOS/Linux Unity 目录布局。

**使用场景：**
- Unity 游戏逆向工程时自动定位主程序集
- 不确定 DLL 具体路径时使用

**AI 对话示例：**
> "在 /path/to/MyGame 里找到 Unity 程序集"
>
> "我有一个 Unity 游戏，帮我找到 Assembly-CSharp.dll"
>
> "这个游戏目录下有哪些托管程序集？"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `gameRootPath` | string | 是 | Unity 游戏根目录或 .app bundle 路径 |

**返回字段：**
- `assemblyCSharpPath` - Assembly-CSharp.dll 完整路径
- `managedDirectory` - Managed 目录路径
- `gameName` - 游戏名称
- `platform` - 检测到的平台（Windows/macOS/Linux）
- `unityVersion` - Unity 版本（如可读取）
- `managedAssemblies[]` - 所有托管 DLL 路径列表

---

## 修改工具

### inject_at_entry

在方法入口注入代码。

**使用场景：**
- 添加日志记录
- 插入调试代码
- 实现方法拦截

**AI 对话示例：**
> "在 Login 方法入口添加日志"
>
> "在 GetUser 方法开始时输出 'GetUser called'"
>
> "给 ProcessOrder 方法添加入口追踪"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `methodFullName` | string | 是 | 完整方法名（类型.方法） |
| `instructions` | object[] | 是 | IL 指令列表 |
| `mvid` | string | 否 | 指定程序集 MVID |

**指令格式示例：**
```json
[
  {"opCode": "ldstr", "stringValue": "Method called"},
  {"opCode": "call", "stringValue": "System.Console::WriteLine"}
]
```

---

### replace_method_body

替换方法体。

**使用场景：**
- 修改方法实现
- 绕过验证逻辑
- 修复问题代码

**AI 对话示例：**
> "把 IsLicenseValid 方法改成永远返回 true"
>
> "让 CheckPermission 方法直接返回 true"
>
> "修改 GetVersion 方法返回 '2.0'"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `methodFullName` | string | 是 | 完整方法名 |
| `instructions` | object[] | 是 | 新的 IL 指令列表 |
| `mvid` | string | 否 | 指定程序集 MVID |

**示例（返回 true）：**
```json
[
  {"opCode": "ldc.i4.1"},
  {"opCode": "ret"}
]
```

---

### add_type

添加新类型。

**使用场景：**
- 向程序集添加新类
- 创建辅助类型
- 注入自定义代码

**AI 对话示例：**
> "添加一个新类 MyApp.Helpers.Logger"
>
> "创建一个名为 DebugHelper 的静态类"
>
> "添加一个实现 IDisposable 的类"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `typeName` | string | 是 | 新类型完整名称 |
| `kind` | string | 否 | 类型种类：class、interface、struct（默认 class） |
| `baseType` | string | 否 | 基类名称 |
| `mvid` | string | 否 | 指定程序集 MVID |

---

### save_assembly

保存修改后的程序集。

**使用场景：**
- 保存所有修改
- 导出修改后的程序集
- 创建修改后的副本

**AI 对话示例：**
> "保存修改后的程序集到 /path/to/Modified.dll"
>
> "把修改保存到 output.dll"
>
> "导出修改后的程序集"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `outputPath` | string | 是 | 输出文件路径 |
| `mvid` | string | 否 | 指定程序集 MVID |

**注意事项：**
- 保存前请确保所有修改已完成
- 建议先备份原始文件
- 输出路径限制在源程序集所在目录内

---

### generate_patch_skeleton

生成 Harmony Patch 骨架代码。

**使用场景：**
- 为游戏 Mod 开发创建 Harmony Patch 模板
- 生成 Prefix/Postfix/Transpiler/Finalizer 补丁
- Unity、RimWorld 等游戏 Modding 工作流

**AI 对话示例：**
> "为 PlayerController.Update 生成 Harmony Prefix 补丁"
>
> "为 Login 方法生成所有类型的补丁"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `typeName` | string | 是 | 完整类型名 |
| `methodName` | string | 是 | 方法名（重载方法使用 "Name(Type1,Type2)" 格式） |
| `patchKinds` | string | 否 | 逗号分隔：Prefix、Postfix、Transpiler、Finalizer（默认 "Prefix,Postfix"） |
| `mvid` | string | 否 | 指定程序集 MVID |

---

### replace_method_body_with_csharp

用 C# 源码替换方法体，而不需要手写 IL 指令。

**使用场景：**
- 不懂 IL 操作码也能修改方法逻辑
- 用简洁的 C# 为方法打桩（如始终返回 true）
- 修改或覆盖现有程序集中的方法实现

**AI 对话示例：**
> "用 C# 把 GetVersion 改为返回 '2.0'"
>
> "让 IsLicenseValid 始终返回 true，用 C# 写"
>
> "替换 ValidateInput 方法体：if (input == null) return false; return input.Length > 0;"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `methodFullName` | string | 是 | 完整方法名，例如 `"MyNamespace.MyClass::MyMethod"` 或 `"MyNamespace.MyClass.MyMethod"` |
| `csharpBody` | string | 是 | C# 方法体（不含签名），例如 `"return x + 1;"` |
| `mvid` | string | 否 | 程序集 MVID 或 alias，省略则使用默认程序集 |
| `usings` | string[] | 否 | 额外的 using 命名空间（默认 System、System.Collections.Generic、System.Linq、System.Text） |
| `allowUnsafe` | bool | 否 | 允许 unsafe C# 代码（默认 false） |
| `backendId` | string | 否 | 指定后端 ID |

**返回字段（成功）：**
- `success` - true
- `message` - 包含替换 IL 指令数的确认信息
- `instructionsReplaced` - 替换后的 IL 指令数

**返回字段（失败）：**
- `success` - false
- `error` - 错误信息
- `diagnostics[]` - Roslyn 编译诊断，格式 `[Severity] ErrorId (line N): message`

**注意：**
- 方法体会用 Roslyn 编译，使用目标方法的参数名和返回类型
- 替换后需调用 `save_assembly` 将变更持久化到磁盘

---

## 后端管理工具

### list_backends

列出所有后端。

**使用场景：**
- 查看可用的分析后端
- 检查后端状态

**AI 对话示例：**
> "列出所有可用的后端"
>
> "有哪些后端？"

---

### register_remote_backend

注册远程后端。

**使用场景：**
- 连接远程分析服务
- 实现分布式分析

**AI 对话示例：**
> "注册远程后端 http://server:5000"
>
> "添加远程分析服务 http://192.168.1.100:5000，命名为 remote-1"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `id` | string | 是 | 唯一后端标识符 |
| `name` | string | 是 | 后端显示名称 |
| `endpoint` | string | 是 | HTTP 端点 URL |
| `apiKey` | string | 否 | 认证用 API 密钥 |
| `timeoutSeconds` | int | 否 | 请求超时秒数（默认 30） |

---

### unregister_backend

注销后端。

**使用场景：**
- 移除不再使用的后端
- 清理会话

**AI 对话示例：**
> "注销 remote-1 后端"
>
> "移除远程后端"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `backendId` | string | 是 | 后端 ID |

---

### set_default_backend

设置默认后端。

**使用场景：**
- 切换主要使用的后端
- 指定默认分析服务

**AI 对话示例：**
> "把 remote-1 设为默认后端"
>
> "切换到本地后端"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `backendId` | string | 是 | 后端 ID |

---

### check_backend_health

检查后端健康状态。

**使用场景：**
- 验证后端是否正常工作
- 诊断连接问题

**AI 对话示例：**
> "检查后端健康状态"
>
> "remote-1 后端正常吗？"
>
> "所有后端的状态如何？"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `backendId` | string | 否 | 后端 ID（为空则检查所有） |

---

## 程序集 Alias 管理工具

Alias 允许你为已加载程序集的 MVID 注册一个简短的可读名称。注册后，所有接受 `mvid` 参数的工具都可以用 alias 代替 32 位 GUID。Alias 会持久化到磁盘（Linux：`~/.local/share/dotnet-mcp/aliases.json`，macOS：`~/Library/Application Support/dotnet-mcp/aliases.json`，Windows：`%LOCALAPPDATA%\dotnet-mcp\aliases.json`），可通过 `instance_restore_persisted` 在跨会话中恢复。

**Alias 命名规则：** 1–32 个字符，字符集 `[A-Za-z0-9_-]`，不能全为数字，不能是保留字（`default`、`local`、`null`）。

---

### register_assembly_alias

为已加载程序集的 MVID 注册短 alias。

**使用场景：**
- 给程序集起一个易记的名字（如 `"main"`）替代 GUID
- 配合 `instance_restore_persisted` 实现跨会话引用

**AI 对话示例：**
> "把当前程序集注册为 alias 'main'"
>
> "把这个程序集 alias 命名为 'target'"
>
> "注册 mvid abc123... 为 'v2'，如已存在则覆盖"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `alias` | string | 是 | 短 alias（1–32 字符，`[A-Za-z0-9_-]`，非保留字） |
| `mvid` | string | 否 | 要绑定的程序集 MVID，省略则使用当前默认程序集 |
| `overwrite` | bool | 否 | 若为 true，覆盖同名 alias（默认 false） |
| `backendId` | string | 否 | 指定后端 ID |

**返回字段：**
- `alias` - 已注册的 alias 名称
- `mvid` - 绑定的 MVID

---

### unregister_assembly_alias

删除已注册的 alias。

**使用场景：**
- 清理过期 alias
- 释放 alias 名称以便重新使用

**AI 对话示例：**
> "删除 alias 'main'"
>
> "取消注册 'target' alias"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `alias` | string | 是 | 要删除的 alias |
| `backendId` | string | 否 | 指定后端 ID |

**注意：**
- 底层程序集保持已加载状态，仅删除 alias 映射。

---

### list_assembly_aliases

列出当前后端所有已注册的 alias。

**使用场景：**
- 查看当前有哪些 alias 可用
- 分析前确认 alias → MVID 映射关系

**AI 对话示例：**
> "列出所有程序集 alias"
>
> "查看已注册的 alias"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `backendId` | string | 否 | 指定后端 ID |

**返回字段：**
- `aliases[]` - `{ alias, mvid }` 对象数组

---

### instance_restore_persisted

从上次会话持久化到磁盘的 alias 条目中重新加载程序集。

**使用场景：**
- 跨会话恢复工作状态，无需手动重新加载程序集
- 从上次对话中恢复已知工作区

**AI 对话示例：**
> "恢复上次会话的程序集"
>
> "从持久化的 alias 加载程序集"

**参数：**
| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `backendId` | string | 否 | 指定后端 ID |

**返回字段：**
- `restoredCount` - 成功恢复的程序集数量

**注意：**
- 无法恢复的条目（文件不存在、路径无效）会自动从持久化文件中删除。

---

## 下一步

- [AI 使用指南](ai-usage-guide.md) - 更多对话示例和使用技巧
- [配置说明](configuration.md) - 了解更多配置选项
