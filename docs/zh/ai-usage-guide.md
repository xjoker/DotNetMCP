# AI 使用指南

[English](../en/ai-usage-guide.md) | 中文

## 概述

DotNet MCP 让 AI 能够分析和修改 .NET 程序集。本指南介绍如何用自然语言与 AI 对话，完成各种逆向工程和程序集修改任务。

## 基本工作流程

### 1. 加载程序集

首先需要加载要分析的 DLL 或 EXE 文件。

**示例对话：**
> 用户：加载 /path/to/MyApp.dll
>
> AI：已加载程序集 MyApp，包含 15 个类型。

**其他说法：**
- "帮我分析这个程序集：C:\Projects\MyLib.dll"
- "打开 ./bin/Debug/net8.0/App.dll"
- "加载 MyApp.dll 并告诉我有什么类型"

**带依赖的加载：**
> 用户：加载 MyApp.dll，依赖目录为 /path/to/libs
>
> AI：已加载程序集，同时从依赖目录解析了 3 个引用。

### 2. 探索类型

了解程序集包含哪些类型。

**示例对话：**
> 用户：列出所有类型
>
> AI：程序集包含以下类型：
> - MyApp.Program (class, 3 methods)
> - MyApp.Services.UserService (class, 8 methods)
> - MyApp.Models.User (class, 5 properties)
> ...

**其他说法：**
- "这个程序集有哪些命名空间？"
- "有哪些类？"
- "列出所有接口"

**搜索特定类型：**
> 用户：搜索名称包含 Controller 的类型
>
> AI：找到 5 个匹配的类型：
> - MyApp.Controllers.HomeController
> - MyApp.Controllers.UserController
> ...

### 3. 查看源码

反编译类型或方法查看实现。

**反编译整个类：**
> 用户：反编译 MyApp.UserService 类
>
> AI：
> ```csharp
> public class UserService
> {
>     private readonly IDatabase _db;
>
>     public User GetUser(int id)
>     {
>         return _db.Query<User>().FirstOrDefault(u => u.Id == id);
>     }
>     ...
> }
> ```

**反编译特定方法：**
> 用户：让我看看 Login 方法的代码
>
> AI：[显示 Login 方法的 C# 代码]

**查看 IL 代码：**
> 用户：显示 Program.Main 方法的 IL 代码
>
> AI：[显示 IL 指令]

### 4. 分析引用关系

理解代码间的调用关系。

**查找类型引用：**
> 用户：找出哪些地方用到了 UserService 类
>
> AI：UserService 被以下位置引用：
> - HomeController.Index() - 字段声明
> - OrderService.CreateOrder() - 方法调用
> ...

**查找方法调用：**
> 用户：谁调用了 ValidateToken 方法？
>
> AI：ValidateToken 被以下方法调用：
> - AuthMiddleware.Invoke()
> - UserController.GetProfile()

**分析调用图：**
> 用户：分析 ProcessOrder 方法的调用图
>
> AI：ProcessOrder 的调用图：
> ```
> ProcessOrder
> ├── ValidateOrder
> │   └── CheckInventory
> ├── CalculateTotal
> │   └── ApplyDiscount
> └── SaveOrder
>     └── Database.Insert
> ```

### 5. 搜索字符串

查找硬编码的字符串。

**示例对话：**
> 用户：搜索包含 "password" 的字符串
>
> AI：找到 3 个匹配的字符串：
> - "Invalid password" (在 AuthService.Login)
> - "password_hash" (在 User.cs)
> - "PasswordValidator" (在 Validators.cs)

**其他说法：**
- "找出所有 URL 字符串"
- "这个程序集里有没有硬编码的 API key？"
- "搜索 http:// 或 https:// 开头的字符串"

### 6. 修改程序集（高级）

注入代码或修改方法。

**在方法入口注入代码：**
> 用户：在 Login 方法入口添加日志
>
> AI：已在 Login 方法入口注入日志代码。修改后方法执行时会先输出日志。

**替换方法实现：**
> 用户：把 IsDebug 方法改成永远返回 true
>
> AI：已将 IsDebug 方法体替换为 `return true;`

**保存修改：**
> 用户：保存修改后的程序集到 /path/to/Modified.dll
>
> AI：已保存修改后的程序集到指定路径。

## 常用任务速查

| 任务 | 示例对话 |
|------|---------|
| 加载程序集 | "加载 xxx.dll" |
| 列出类型 | "列出所有类型" / "有哪些类" |
| 搜索类型 | "搜索 Service" / "找名称包含 Controller 的类" |
| 反编译类 | "反编译 MyClass" / "看看 MyClass 的源码" |
| 反编译方法 | "反编译 MyClass.DoWork 方法" |
| 查找引用 | "谁用到了 MyClass" / "MyMethod 被哪里调用" |
| 调用图 | "分析 Main 的调用图" |
| 搜索字符串 | "搜索 password" / "找 http 开头的字符串" |
| 控制流图 | "显示 xxx 方法的控制流图" |
| 注入代码 | "在 xxx 方法入口加日志" |
| 保存修改 | "保存到 /path/output.dll" |

## 进阶技巧

### 组合命令

可以在一句话中请求多个操作：
> "加载 MyApp.dll，找出所有 Service 类，然后反编译 UserService"

### 追问细节

AI 会记住上下文，可以追问：
> 用户：反编译 UserService
>
> AI：[显示代码]
>
> 用户：GetUser 方法有什么问题吗？
>
> 用户：这个方法调用了哪些其他方法？

### 指定程序集

当加载了多个程序集时，可以指定目标：
> 用户：在 MyApp 程序集中搜索 Error
>
> 用户：反编译 MyLib 中的 Helper 类

### 使用完整类型名

对于重名类型，使用完整命名空间：
> 用户：反编译 MyApp.Services.UserService（而不是 UserService）

### 控制结果数量

对于大型程序集：
> 用户：列出前 20 个类型
>
> 用户：搜索 Service，只显示 10 个结果

## 实际应用场景

### 场景一：分析第三方库

```
用户: 加载 ThirdParty.dll

用户: 有哪些公开的 API？

用户: AuthClient 类怎么用？反编译看看

用户: Authenticate 方法需要什么参数？
```

### 场景二：调试问题

```
用户: 加载出问题的 MyApp.dll

用户: 搜索包含 "Exception" 的字符串

用户: 反编译 ErrorHandler 类

用户: 谁调用了 HandleError 方法？
```

### 场景三：安全审计

```
用户: 加载 Target.dll

用户: 搜索 password、secret、key 相关字符串

用户: 找出所有调用了 SQL 相关方法的地方

用户: 反编译 DatabaseHelper 看看有没有 SQL 注入风险
```

### 场景四：代码修改

```
用户: 加载 App.dll

用户: 反编译 LicenseChecker 类

用户: 把 IsLicenseValid 方法改成永远返回 true

用户: 保存到 App_Modified.dll
```

## 进阶场景

### 场景五：检测程序集是否被混淆

加载程序集后，先用 `detect_obfuscation` 判断是否需要去混淆，再决定后续策略：

```
用户: 加载 SuspiciousApp.dll

用户: 这个程序集被混淆了吗？

AI: [调用 detect_obfuscation]

    检测结果：混淆评分 87/100，置信度 High。
    识别到混淆器：ConfuserEx。
    主要指标：大量单字符类型名（如 a、b、c）、控制流平坦化方法 42 个、代理方法 18 个。
    建议使用对应的反混淆工具处理后再分析。
```

### 场景六：分析继承关系

当需要理解类型体系时，组合使用继承相关工具：

```
用户: IRepository 接口有哪些实现？

AI: [调用 get_implementations interfaceTypeName="MyApp.IRepository"]

    找到 3 个直接实现：
    - MyApp.Data.SqlRepository
    - MyApp.Data.MongoRepository
    - MyApp.Data.InMemoryRepository

用户: SqlRepository 的基类链是什么？

AI: [调用 find_base_types typeName="MyApp.Data.SqlRepository"]

    基类链：
    - MyApp.Data.BaseRepository (internal)
    - System.Object (external)
    实现接口：IRepository、IDisposable

用户: BaseRepository 还有哪些派生类？

AI: [调用 find_derived_types typeName="MyApp.Data.BaseRepository"]

    找到 4 个派生类型：SqlRepository、MongoRepository、InMemoryRepository、CachedRepository
```

### 场景七：Unity 游戏逆向

分析 Unity 游戏时，先用 `detect_unity_assembly` 自动定位程序集：

```
用户: 帮我分析 /Users/me/Games/MyGame 这个 Unity 游戏

AI: [调用 detect_unity_assembly gameRootPath="/Users/me/Games/MyGame"]

    找到 Unity 程序集：
    - Assembly-CSharp.dll: /Users/me/Games/MyGame/MyGame.app/Contents/Resources/Data/Managed/Assembly-CSharp.dll
    - 游戏名称：MyGame
    - Unity 版本：2022.3.10f1
    - 共 47 个托管程序集

用户: 加载 Assembly-CSharp.dll

AI: [调用 load_assembly path="..."]

用户: 找出所有 Player 相关的类型

AI: [调用 search_types keyword="Player"]
```

### 场景八：依赖图可视化

用依赖图了解代码结构，复制 Mermaid 输出即可渲染：

```
用户: 显示这个程序集的依赖图

AI: [调用 get_dependency_graph level="assembly"]

    程序集依赖图（8 个节点，12 条边）：
    ```mermaid
    graph LR
      MyApp --> Newtonsoft.Json
      MyApp --> Microsoft.EntityFrameworkCore
      ...
    ```

用户: 展示 UserService 的类型依赖关系，深度 2

AI: [调用 get_dependency_graph level="type" rootType="MyApp.Services.UserService" maxDepth=2]

    类型依赖图（Mermaid）：...
```

## 用 Alias 简化工作流

不必在每次工具调用中重复完整 MVID，注册一次短 alias 就可以到处使用。

```
用户: 加载 /path/to/GameLib.dll，然后注册为 alias 'game'

AI: [调用 load_assembly path="/path/to/GameLib.dll"]
    [调用 register_assembly_alias alias="game"]

    已加载程序集 GameLib.dll，并注册为 alias 'game'。
    之后凡是需要填 mvid 的地方都可以使用 'game'。

用户: 用 alias 'game' 搜索名称包含 Controller 的类型

AI: [调用 search_types keyword="Controller" mvid="game"]

    找到 5 个匹配 'Controller' 的类型：
    - GameLib.PlayerController
    - GameLib.EnemyController
    ...

# 下次会话恢复
用户: 恢复上次的程序集

AI: [调用 instance_restore_persisted]

    已恢复 1 个程序集，alias 'game' → GameLib.dll 可用。
```

## 用 C# 而非 IL 修改方法

使用 `replace_method_body_with_csharp` 无需编写原始 IL 操作码即可修改方法逻辑。

```
用户: 用 C# 把 IsTrialExpired 改为始终返回 false

AI: [调用 replace_method_body_with_csharp
     methodFullName="MyApp.Licensing.LicenseManager::IsTrialExpired"
     csharpBody="return false;"]

    方法体已替换，共 2 条 IL 指令。

用户: 保存修改

AI: [调用 save_assembly outputPath="/path/to/MyApp.Patched.dll"]

    程序集已保存到 MyApp.Patched.dll。

# 编译失败时会返回诊断信息
用户: 替换 GetConfig，让它返回一个特定字符串

AI: [调用 replace_method_body_with_csharp
     methodFullName="MyApp.Config::GetConfig"
     csharpBody="return Cofig.Default;"]  ← 拼写错误

    编译失败：
    [Error] CS0103 (line 1): The name 'Cofig' does not exist in the current context
```

## 大型程序集预热索引

对于大型程序集（>5 MB），分析前预先构建索引可以显著减少每次查询的延迟。

```
用户: 加载游戏程序集，在开始分析前先预热索引

AI: [调用 load_assembly path="/path/to/Assembly-CSharp.dll"]
    [调用 warm_index typeIndex=true memberIndex=true maxSeconds=30]

    已加载 Assembly-CSharp.dll（8.4 MB，2847 个类型）。
    索引预热完成：2847 个类型、41320 个成员，耗时 18.4 秒。

用户: 现在搜索所有名为 Update 的方法

AI: [调用 search_types keyword="Update"]  ← 直接命中缓存索引，响应即时

    ...
```

## 注意事项

1. **路径格式**：使用绝对路径更可靠
2. **类型名称**：优先使用完整命名空间
3. **大型程序集**：使用 limit 参数控制结果数量
4. **依赖解析**：使用 searchPaths 指定依赖目录
5. **修改前备份**：修改程序集前建议先备份原文件
6. **Alias 跨会话持久化**：配合 `register_assembly_alias` + `instance_restore_persisted` 可避免每次会话重新加载程序集

## 下一步

- [工具参考](tools-reference.md) - 查看所有工具的详细参数
- [配置说明](configuration.md) - 了解更多配置选项
