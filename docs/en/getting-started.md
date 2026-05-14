# Getting Started

English | [中文](../zh/getting-started.md)

## Requirements

- **.NET 10.0 SDK** or higher
- Operating System: Windows, macOS, or Linux
- Claude Desktop (for AI interaction)

## Installation

### Option 1: Download Pre-built Binary (Recommended — no .NET SDK needed)

1. Go to [GitHub Releases](https://github.com/xjoker/DotNetMCP/releases) and download the zip for your platform:

   | Platform | File |
   |----------|------|
   | Windows x64 | `DotNetMcp-win-x64.zip` |
   | Linux x64 | `DotNetMcp-linux-x64.zip` |
   | Linux ARM64 | `DotNetMcp-linux-arm64.zip` |
   | macOS x64 | `DotNetMcp-osx-x64.zip` |
   | macOS ARM64 (Apple Silicon) | `DotNetMcp-osx-arm64.zip` |

2. Extract to any directory. You will find a single self-contained executable: `DotNetMcp.Server` (or `DotNetMcp.Server.exe` on Windows).

3. **macOS/Linux only** — make it executable:
   ```bash
   chmod +x /path/to/DotNetMcp.Server
   ```

4. Configure Claude Desktop (see [Claude Desktop Configuration](#claude-desktop-configuration) below).

### Option 2: Build from Source (.NET 10.0 SDK required)

#### Windows

1. **Install .NET 10.0 SDK**

   Download and install from [Microsoft .NET Download Page](https://dotnet.microsoft.com/download).

   Verify installation:
   ```powershell
   dotnet --version
   ```

2. **Clone and Build**
   ```powershell
   git clone https://github.com/xjoker/DotNetMCP.git
   cd DotNetMCP
   dotnet build
   ```

3. **Configure Claude Desktop**

   Edit configuration file `%APPDATA%\Claude\claude_desktop_config.json`:
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

1. **Install .NET SDK**
   ```bash
   brew install dotnet
   ```

   Or download installer from [Microsoft website](https://dotnet.microsoft.com/download).

2. **Clone and Build**
   ```bash
   git clone https://github.com/xjoker/DotNetMCP.git
   cd DotNetMCP
   dotnet build
   ```

3. **Configure Claude Desktop**

   Edit configuration file `~/Library/Application Support/Claude/claude_desktop_config.json`:
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

1. **Install .NET SDK**

   Ubuntu/Debian:
   ```bash
   wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb
   sudo dpkg -i packages-microsoft-prod.deb
   sudo apt update
   sudo apt install dotnet-sdk-10.0
   ```

2. **Clone and Build**
   ```bash
   git clone https://github.com/xjoker/DotNetMCP.git
   cd DotNetMCP
   dotnet build
   ```

3. **Configure Claude Desktop**

   Edit configuration file `~/.config/Claude/claude_desktop_config.json`:
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

## Claude Desktop Configuration

### Using Pre-built Binary (Option 1)

| OS | Config file location |
|----|----------------------|
| Windows | `%APPDATA%\Claude\claude_desktop_config.json` |
| macOS | `~/Library/Application Support/Claude/claude_desktop_config.json` |
| Linux | `~/.config/Claude/claude_desktop_config.json` |

```json
{
  "mcpServers": {
    "dotnet-mcp": {
      "command": "/path/to/DotNetMcp.Server",
      "args": ["--stdio"]
    }
  }
}
```

Replace `/path/to/DotNetMcp.Server` with the actual path to the extracted executable.

### Using Source Build (Option 2)

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

### Using Claude CLI

```bash
# Stdio mode — pre-built binary
claude mcp add dotnet-mcp -- /path/to/DotNetMcp.Server --stdio

# HTTP mode — start server first
dotnet run --project src/DotNetMcp.Server &
claude mcp add dotnet-mcp --transport http --url http://localhost:5000/mcp
```

## Running Modes

### Stdio Mode (Recommended for Claude Desktop)

```bash
dotnet run --project src/DotNetMcp.Server -- --stdio
```

This mode communicates with Claude Desktop via standard input/output.

### HTTP Mode

```bash
dotnet run --project src/DotNetMcp.Server
```

The service will start at `http://localhost:5000`, supporting MCP endpoints via HTTP.

## How to Talk to AI

After configuration, restart Claude Desktop. You can then use natural language to have AI analyze assemblies.

### Basic Conversation Examples

**Load an assembly:**
> "Load /path/to/MyApp.dll for analysis"

**List all types:**
> "List all types in this assembly"

**Search for specific types:**
> "Search for types containing Service in their name"

**Decompile to view source:**
> "Decompile the MyNamespace.UserService class, show me its implementation"

**Analyze call relationships:**
> "Analyze what methods the Main method calls"

**Search strings:**
> "Search for strings containing password"

### Complete Workflow Example

```
User: Load /Users/me/Projects/MyApp/bin/Debug/MyApp.dll

AI: Loaded assembly MyApp.dll, containing 25 types.

User: What Service classes are there?

AI: Found the following Service classes:
    - MyApp.Services.UserService (8 methods)
    - MyApp.Services.OrderService (12 methods)
    - MyApp.Services.PaymentService (6 methods)

User: Decompile UserService

AI: [Shows complete C# source code for UserService]

User: What other methods does GetUser call?

AI: [Shows call graph for GetUser]
```

## FAQ

### Q: Claude says it can't find the tools?

**A:** Check the following:
1. Ensure the configuration file path is correct
2. Restart Claude Desktop
3. Verify .NET SDK is properly installed (run `dotnet --version`)

### Q: Failed to load assembly?

**A:**
1. Ensure the DLL path is correct and file exists
2. Use absolute paths instead of relative paths
3. If there are dependencies, use the `searchPaths` parameter to specify dependency directories:
   > "Load MyApp.dll with dependency directory /path/to/libs"

### Q: Decompilation results are incomplete?

**A:**
1. Ensure the assembly is not obfuscated
2. Some types may require full namespace, e.g., `MyNamespace.MyClass`

### Q: How to avoid repeating the MVID in every command?

**A:**
After loading, register a short alias:
> "Register the loaded assembly as alias 'main'"

Then use `'main'` wherever a `mvid` is required. Use `instance_restore_persisted` to reload the alias in the next session.

### Q: How to analyze multiple assemblies?

**A:**
1. Load multiple assemblies sequentially
2. Use `list_assemblies` to see loaded assemblies
3. Specify target assembly when analyzing (by MVID or alias)

### Q: Path issues on Windows?

**A:**
1. Use double backslashes or forward slashes: `C:\\Projects\\MyApp.dll` or `C:/Projects/MyApp.dll`
2. Use quotes when path contains spaces

## Next Steps

- [AI Usage Guide](ai-usage-guide.md) - Detailed AI conversation examples and tips
- [Configuration](configuration.md) - Learn more configuration options
- [Tools Reference](tools-reference.md) - View all MCP tools details
