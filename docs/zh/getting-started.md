# 快速开始

[English](../en/getting-started.md) | 中文

## 环境要求

- **.NET 10.0 SDK** 或更高版本
- 操作系统：Windows、macOS 或 Linux
- Claude Desktop（用于 AI 交互）

## 安装步骤

### 方式一：从源码编译（推荐）

#### Windows

1. **安装 .NET 10.0 SDK**

   从 [Microsoft .NET 下载页面](https://dotnet.microsoft.com/download) 下载并安装。

   验证安装：
   ```powershell
   dotnet --version
   ```

2. **克隆仓库并编译**
   ```powershell
   git clone https://github.com/xjoker/DotNetMCP.git
   cd DotNetMCP
   dotnet build
   ```

3. **配置 Claude Desktop**

   编辑配置文件 `%APPDATA%\Claude\claude_desktop_config.json`：
   ```json
   {
     "mcpServers": {
       "dotnet-mcp": {
         "command": "dotnet",
         "args": [
           "run",
           "--project",
           "C:\\path\\to\\DotNetMCP\\src\\DotNetMcp.Server",
           "--",
           "--stdio"
         ]
       }
     }
   }
   ```

#### macOS

1. **安装 .NET SDK**
   ```bash
   brew install dotnet
   ```

   或从 [Microsoft 官网](https://dotnet.microsoft.com/download) 下载安装包。

2. **克隆仓库并编译**
   ```bash
   git clone https://github.com/xjoker/DotNetMCP.git
   cd DotNetMCP
   dotnet build
   ```

3. **配置 Claude Desktop**

   编辑配置文件 `~/Library/Application Support/Claude/claude_desktop_config.json`：
   ```json
   {
     "mcpServers": {
       "dotnet-mcp": {
         "command": "dotnet",
         "args": [
           "run",
           "--project",
           "/path/to/DotNetMCP/src/DotNetMcp.Server",
           "--",
           "--stdio"
         ]
       }
     }
   }
   ```

#### Linux

1. **安装 .NET SDK**

   Ubuntu/Debian：
   ```bash
   wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb
   sudo dpkg -i packages-microsoft-prod.deb
   sudo apt update
   sudo apt install dotnet-sdk-10.0
   ```

2. **克隆仓库并编译**
   ```bash
   git clone https://github.com/xjoker/DotNetMCP.git
   cd DotNetMCP
   dotnet build
   ```

3. **配置 Claude Desktop**

   编辑配置文件 `~/.config/Claude/claude_desktop_config.json`：
   ```json
   {
     "mcpServers": {
       "dotnet-mcp": {
         "command": "dotnet",
         "args": [
           "run",
           "--project",
           "/path/to/DotNetMCP/src/DotNetMcp.Server",
           "--",
           "--stdio"
         ]
       }
     }
   }
   ```

### 方式二：使用已编译的可执行文件

1. **编译发布版本**
   ```bash
   dotnet publish src/DotNetMcp.Server -c Release -o ./publish
   ```

2. **配置 Claude Desktop**
   ```json
   {
     "mcpServers": {
       "dotnet-mcp": {
         "command": "/path/to/publish/DotNetMcp.Server",
         "args": ["--stdio"]
       }
     }
   }
   ```

## 运行模式

### Stdio 模式（推荐用于 Claude Desktop）

```bash
dotnet run --project src/DotNetMcp.Server -- --stdio
```

此模式通过标准输入/输出与 Claude Desktop 通信。

### HTTP 模式

```bash
dotnet run --project src/DotNetMcp.Server
```

服务将在 `http://localhost:5000` 启动，支持通过 HTTP 访问 MCP 端点。

## 如何与 AI 对话

配置完成后，重启 Claude Desktop，然后你可以用自然语言让 AI 分析程序集。

### 基本对话示例

**加载程序集：**
> "加载 /path/to/MyApp.dll 进行分析"

**查看所有类型：**
> "列出这个程序集里所有的类型"

**搜索特定类型：**
> "搜索名称包含 Service 的类型"

**反编译查看源码：**
> "反编译 MyNamespace.UserService 类，让我看看它的实现"

**分析调用关系：**
> "分析 Main 方法都调用了哪些方法"

**搜索字符串：**
> "搜索包含 password 的字符串"

### 完整工作流示例

```
用户: 加载 /Users/me/Projects/MyApp/bin/Debug/MyApp.dll

AI: 已加载程序集 MyApp.dll，包含 25 个类型。

用户: 有哪些 Service 类？

AI: 找到以下 Service 类：
    - MyApp.Services.UserService (8 methods)
    - MyApp.Services.OrderService (12 methods)
    - MyApp.Services.PaymentService (6 methods)

用户: 反编译 UserService

AI: [显示 UserService 的完整 C# 源码]

用户: GetUser 方法调用了哪些其他方法？

AI: [显示 GetUser 的调用图]
```

## 常见问题

### Q: Claude 提示找不到工具？

**A:** 检查以下几点：
1. 确保配置文件路径正确
2. 重启 Claude Desktop
3. 检查 .NET SDK 是否正确安装（运行 `dotnet --version`）

### Q: 加载程序集失败？

**A:**
1. 确保 DLL 路径正确且文件存在
2. 使用绝对路径而非相对路径
3. 如有依赖，使用 `searchPaths` 参数指定依赖目录：
   > "加载 MyApp.dll，依赖目录为 /path/to/libs"

### Q: 反编译结果不完整？

**A:**
1. 确保程序集未被混淆
2. 某些类型可能需要完整的命名空间，例如 `MyNamespace.MyClass`

### Q: 每次命令都要输入 MVID，有没有更简单的方式？

**A:**
加载程序集后注册一个短 alias：
> "把已加载的程序集注册为 alias 'main'"

之后凡是需要 `mvid` 参数的地方，直接填 `'main'` 即可。使用 `instance_restore_persisted` 可在下次会话中恢复 alias。

### Q: 如何分析多个程序集？

**A:**
1. 依次加载多个程序集
2. 使用 `list_assemblies` 查看已加载的程序集
3. 在分析时指定目标程序集（MVID 或 alias 均可）

### Q: Windows 上路径问题？

**A:**
1. 使用双反斜杠或正斜杠：`C:\\Projects\\MyApp.dll` 或 `C:/Projects/MyApp.dll`
2. 路径中有空格时使用引号

## 下一步

- [AI 使用指南](ai-usage-guide.md) - 详细的 AI 对话示例和技巧
- [配置说明](configuration.md) - 了解更多配置选项
- [工具参考](tools-reference.md) - 查看所有 MCP 工具详情
