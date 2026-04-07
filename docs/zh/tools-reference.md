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

## 下一步

- [AI 使用指南](ai-usage-guide.md) - 更多对话示例和使用技巧
- [配置说明](configuration.md) - 了解更多配置选项
