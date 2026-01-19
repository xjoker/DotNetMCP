# DotNet MCP 技术栈与版本

> 最后更新: 2026-01-19

## 📦 最新 LTS 版本

| 组件 | 版本 | 发布日期 | 支持到期 |
|-----|------|---------|---------|
| **Python** | **3.14** | 2025-10 | 2030-10 |
| **.NET** | **10 LTS** | 2025-11 | 2028-11 |
| **Mono.Cecil** | **0.11.6** | 2024-10 | - |
| **ICSharpCode.Decompiler** | **9.1.0** | 2025-04 | - |
| **Microsoft.CodeAnalysis.CSharp** | **5.0.0** | 2025-11 | - |
| **FastMCP** | **2.0+** | - | - |
| **httpx** | **0.28.1+** | - | - |

---

## 🐍 Python MCP Server

### 运行时
| 项目 | 版本 | 说明 |
|-----|------|------|
| Python | **≥ 3.12** | 推荐 3.14 (最新稳定) |
| venv | 内置 | 虚拟环境 |

### 核心依赖
| 包名 | 版本约束 | 用途 |
|-----|---------|------|
| fastmcp | `>=2.0.0,<3.0` | MCP 协议实现 |
| httpx | `>=0.28.1` | 异步 HTTP 客户端 |
| tomli | `>=2.0.0` | TOML 解析 (Python < 3.11) |

**requirements.txt**:
```txt
fastmcp>=2.0.0,<3.0
httpx>=0.28.1
tomli>=2.0.0
```

---

## 🔷 C# 后端服务

### 运行时
| 项目 | 版本 | 说明 |
|-----|------|------|
| .NET SDK | **10.0** | LTS，支持到 2028-11 |
| ASP.NET Core | **10.0** | 随 .NET 10 |

### NuGet 依赖
| 包名 | 版本 | 用途 |
|-----|------|------|
| Mono.Cecil | **0.11.6** | ECMA-335 元数据读写 |
| ICSharpCode.Decompiler | **9.1.0.7988** | ILSpy 反编译引擎（稳定版） |
| Microsoft.CodeAnalysis.CSharp | **5.0.0** | Roslyn C# 编译器 |

**DotNetMcp.Backend.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="Mono.Cecil" Version="0.11.6" />
    <PackageReference Include="ICSharpCode.Decompiler" Version="9.1.0.7988" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="5.0.0" />
  </ItemGroup>
</Project>
```

---

## 🐳 Docker

| 组件 | 版本 |
|-----|------|
| Docker Engine | ≥ 20.10 |
| Docker Compose | ≥ 3.8 |

### 基础镜像
| 服务 | 镜像 |
|-----|------|
| Python MCP Server | `python:3.14-slim` |
| C# 后端服务 | `mcr.microsoft.com/dotnet/aspnet:10.0` |

---

## 🎯 支持的 .NET 平台

| 平台 | 版本范围 | 状态 | 说明 |
|-----|---------|------|------|
| .NET Framework | 2.0 - 4.8.x | ✅ | 完整支持 |
| .NET Core | 1.0 - 3.1 | ✅ | 完整支持 |
| .NET | 5 - 10+ | ✅ | 完整支持，推荐 10 LTS |
| .NET Standard | 1.x - 2.1 | ✅ | 完整支持 |
| Mono | 各版本 | ✅ | 完整支持 |
| Xamarin / MAUI | - | ✅ | 完整支持 |
| Unity IL2CPP | - | ❌ | 不支持（非标准 IL） |
| NativeAOT | - | ❌ | 不支持（无元数据） |

---

## 🔄 版本选择理由

| 技术 | 选择版本 | 理由 |
|-----|---------|------|
| **Python 3.12+** | 最低 3.12，推荐 3.14 | FastMCP 兼容，模式匹配、性能优化 |
| **.NET 10 LTS** | 10.0 | 最新 LTS，支持到 2028-11，性能最优 |
| **Mono.Cecil 0.11.6** | 0.11.6 | 最新稳定版，完整 .NET 10 支持 |
| **ILSpy 9.1** | 9.1.0 (稳定) | 生产就绪，C# 13 支持 |
| **Roslyn 5.0** | 5.0.0 | 与 .NET 10 SDK 对齐 |

---

## 📅 支持时间线

```
2026-01 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 2030+
        │
Python 3.14  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━▶ 2030-10
.NET 10 LTS  ━━━━━━━━━━━━━━━━▶ 2028-11
.NET 8 LTS   ━━━▶ 2026-11 (即将 EOL)
Python 3.10  ━━━▶ 2026-10 (即将 EOL)
```

---

## ⚠️ 重要提示

### 预览版 vs 稳定版
- **ILSpy 10.0-preview2**: 支持 C# 14，基于 .NET 10，但尚未稳定
- **ILSpy 9.1.0**: 当前项目采用的稳定版本

### 升级路径
从旧版本迁移：
1. 升级 .NET SDK: `dotnet --version` 确认 ≥ 10.0
2. 升级 Python: `python --version` 确认 ≥ 3.12
3. 更新 NuGet 包: `dotnet restore`
4. 更新 Python 包: `pip install -r requirements.txt --upgrade`
