# AI Usage Guide

English | [中文](../zh/ai-usage-guide.md)

## Overview

DotNet MCP enables AI to analyze and modify .NET assemblies. This guide explains how to use natural language to interact with AI for various reverse engineering and assembly modification tasks.

## Basic Workflow

### 1. Load Assembly

First, load the DLL or EXE file you want to analyze.

**Example conversation:**
> User: Load /path/to/MyApp.dll
>
> AI: Loaded assembly MyApp, containing 15 types.

**Alternative phrasings:**
- "Analyze this assembly: C:\Projects\MyLib.dll"
- "Open ./bin/Debug/net8.0/App.dll"
- "Load MyApp.dll and tell me what types it contains"

**Loading with dependencies:**
> User: Load MyApp.dll with dependency directory /path/to/libs
>
> AI: Loaded assembly, resolved 3 references from dependency directory.

### 2. Explore Types

Understand what types the assembly contains.

**Example conversation:**
> User: List all types
>
> AI: The assembly contains the following types:
> - MyApp.Program (class, 3 methods)
> - MyApp.Services.UserService (class, 8 methods)
> - MyApp.Models.User (class, 5 properties)
> ...

**Alternative phrasings:**
- "What namespaces does this assembly have?"
- "What classes are there?"
- "List all interfaces"

**Search for specific types:**
> User: Search for types containing Controller
>
> AI: Found 5 matching types:
> - MyApp.Controllers.HomeController
> - MyApp.Controllers.UserController
> ...

### 3. View Source Code

Decompile types or methods to view implementation.

**Decompile entire class:**
> User: Decompile the MyApp.UserService class
>
> AI:
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

**Decompile specific method:**
> User: Show me the code for the Login method
>
> AI: [Shows Login method C# code]

**View IL code:**
> User: Show the IL code for Program.Main method
>
> AI: [Shows IL instructions]

### 4. Analyze References

Understand code relationships.

**Find type references:**
> User: Find where UserService class is used
>
> AI: UserService is referenced in:
> - HomeController.Index() - field declaration
> - OrderService.CreateOrder() - method call
> ...

**Find method calls:**
> User: Who calls the ValidateToken method?
>
> AI: ValidateToken is called by:
> - AuthMiddleware.Invoke()
> - UserController.GetProfile()

**Analyze call graph:**
> User: Analyze the call graph of ProcessOrder method
>
> AI: Call graph for ProcessOrder:
> ```
> ProcessOrder
> ├── ValidateOrder
> │   └── CheckInventory
> ├── CalculateTotal
> │   └── ApplyDiscount
> └── SaveOrder
>     └── Database.Insert
> ```

### 5. Search Strings

Find hardcoded strings.

**Example conversation:**
> User: Search for strings containing "password"
>
> AI: Found 3 matching strings:
> - "Invalid password" (in AuthService.Login)
> - "password_hash" (in User.cs)
> - "PasswordValidator" (in Validators.cs)

**Alternative phrasings:**
- "Find all URL strings"
- "Are there any hardcoded API keys in this assembly?"
- "Search for strings starting with http:// or https://"

### 6. Modify Assembly (Advanced)

Inject code or modify methods.

**Inject code at method entry:**
> User: Add logging at Login method entry
>
> AI: Injected logging code at Login method entry. The modified method will output logs before execution.

**Replace method implementation:**
> User: Change IsDebug method to always return true
>
> AI: Replaced IsDebug method body with `return true;`

**Save modifications:**
> User: Save modified assembly to /path/to/Modified.dll
>
> AI: Saved modified assembly to the specified path.

## Quick Reference

| Task | Example Conversation |
|------|---------------------|
| Load assembly | "Load xxx.dll" |
| List types | "List all types" / "What classes are there" |
| Search types | "Search Service" / "Find classes containing Controller" |
| Decompile class | "Decompile MyClass" / "Show MyClass source code" |
| Decompile method | "Decompile MyClass.DoWork method" |
| Find references | "Who uses MyClass" / "Where is MyMethod called" |
| Call graph | "Analyze Main's call graph" |
| Search strings | "Search password" / "Find strings starting with http" |
| Control flow graph | "Show control flow graph for xxx method" |
| Inject code | "Add logging at xxx method entry" |
| Save changes | "Save to /path/output.dll" |

## Advanced Tips

### Combine Commands

Request multiple operations in one message:
> "Load MyApp.dll, find all Service classes, then decompile UserService"

### Follow-up Questions

AI remembers context, so you can ask follow-ups:
> User: Decompile UserService
>
> AI: [Shows code]
>
> User: Any issues with GetUser method?
>
> User: What other methods does it call?

### Specify Assembly

When multiple assemblies are loaded, specify the target:
> User: Search for Error in MyApp assembly
>
> User: Decompile Helper class from MyLib

### Use Full Type Names

For types with same names, use full namespace:
> User: Decompile MyApp.Services.UserService (instead of just UserService)

### Control Result Count

For large assemblies:
> User: List first 20 types
>
> User: Search Service, show only 10 results

## Practical Scenarios

### Scenario 1: Analyze Third-Party Library

```
User: Load ThirdParty.dll

User: What public APIs are available?

User: How do I use the AuthClient class? Decompile it

User: What parameters does the Authenticate method need?
```

### Scenario 2: Debug Issues

```
User: Load the problematic MyApp.dll

User: Search for strings containing "Exception"

User: Decompile ErrorHandler class

User: Who calls HandleError method?
```

### Scenario 3: Security Audit

```
User: Load Target.dll

User: Search for strings related to password, secret, key

User: Find all places that call SQL-related methods

User: Decompile DatabaseHelper to check for SQL injection risks
```

### Scenario 4: Code Modification

```
User: Load App.dll

User: Decompile LicenseChecker class

User: Change IsLicenseValid method to always return true

User: Save to App_Modified.dll
```

## Advanced Scenarios

### Scenario 5: Detect Whether an Assembly is Obfuscated

After loading an assembly, use `detect_obfuscation` to assess whether de-obfuscation is required before proceeding:

```
User: Load SuspiciousApp.dll

User: Is this assembly obfuscated?

AI: [Call detect_obfuscation]

    Result: Obfuscation score 87/100, confidence High.
    Detected obfuscator: ConfuserEx.
    Top indicators: many single-character type names (a, b, c), 42 control-flow-flattened methods, 18 proxy methods.
    Recommend using a matching de-obfuscator before further analysis.
```

### Scenario 6: Analyze Inheritance Relationships

When you need to understand a type hierarchy, combine the inheritance tools:

```
User: What types implement IRepository?

AI: [Call get_implementations interfaceTypeName="MyApp.IRepository"]

    Found 3 direct implementations:
    - MyApp.Data.SqlRepository
    - MyApp.Data.MongoRepository
    - MyApp.Data.InMemoryRepository

User: What is the base class chain of SqlRepository?

AI: [Call find_base_types typeName="MyApp.Data.SqlRepository"]

    Base classes: BaseRepository (internal) → System.Object (external)
    Interfaces implemented: IRepository, IDisposable

User: Are there any other types derived from BaseRepository?

AI: [Call find_derived_types typeName="MyApp.Data.BaseRepository"]

    Found 4 derived types: SqlRepository, MongoRepository, InMemoryRepository, CachedRepository
```

### Scenario 7: Unity Game Reverse Engineering

When analyzing a Unity game, use `detect_unity_assembly` to auto-locate the main assembly:

```
User: Help me analyze the Unity game at /Users/me/Games/MyGame

AI: [Call detect_unity_assembly gameRootPath="/Users/me/Games/MyGame"]

    Found Unity assembly:
    - Assembly-CSharp.dll: /Users/me/Games/MyGame/MyGame.app/Contents/Resources/Data/Managed/Assembly-CSharp.dll
    - Game name: MyGame
    - Unity version: 2022.3.10f1
    - 47 managed assemblies found

User: Load Assembly-CSharp.dll

AI: [Call load_assembly path="..."]

User: Find all Player-related types

AI: [Call search_types keyword="Player"]
```

### Scenario 8: Dependency Graph Visualization

Use the dependency graph to understand code structure. Copy the Mermaid output to any Mermaid renderer:

```
User: Show the dependency graph for this assembly

AI: [Call get_dependency_graph level="assembly"]

    Assembly dependency graph (8 nodes, 12 edges):
    ```mermaid
    graph LR
      MyApp --> Newtonsoft.Json
      MyApp --> Microsoft.EntityFrameworkCore
      ...
    ```

User: Show type dependencies for UserService at depth 2

AI: [Call get_dependency_graph level="type" rootType="MyApp.Services.UserService" maxDepth=2]

    Type dependency graph (Mermaid): ...
```

## Using Aliases to Simplify Workflows

Instead of repeating the full MVID in every tool call, register a short alias once and use it everywhere.

```
User: Load /path/to/GameLib.dll, then register it as alias 'game'

AI: [Call load_assembly path="/path/to/GameLib.dll"]
    [Call register_assembly_alias alias="game"]

    Loaded assembly GameLib.dll. Registered as alias 'game'. You can now use 'game' wherever a mvid is required.

User: Find all types named Controller using alias 'game'

AI: [Call search_types keyword="Controller" mvid="game"]

    Found 5 types matching 'Controller':
    - GameLib.PlayerController
    - GameLib.EnemyController
    ...

# Restore on next session
User: Restore my previous assemblies

AI: [Call instance_restore_persisted]

    Restored 1 assembly. Alias 'game' → GameLib.dll is available again.
```

## Patching Methods with C# Instead of IL

Use `replace_method_body_with_csharp` to patch methods without writing raw IL opcodes.

```
User: Make IsTrialExpired always return false, write it as C#

AI: [Call replace_method_body_with_csharp
     methodFullName="MyApp.Licensing.LicenseManager::IsTrialExpired"
     csharpBody="return false;"]

    Method body replaced with 2 IL instructions.

User: Save the changes

AI: [Call save_assembly outputPath="/path/to/MyApp.Patched.dll"]

    Assembly saved to MyApp.Patched.dll.

# If compilation fails, diagnostics are returned
User: Replace GetConfig to return a specific string

AI: [Call replace_method_body_with_csharp
     methodFullName="MyApp.Config::GetConfig"
     csharpBody="return Cofig.Default;"]  ← typo

    Compilation failed:
    [Error] CS0103 (line 1): The name 'Cofig' does not exist in the current context
```

## Pre-warming Index for Large Assemblies

For large assemblies (>5 MB), pre-building indexes before heavy analysis reduces per-query latency.

```
User: Load the game assembly, then pre-warm indexes before I start analysis

AI: [Call load_assembly path="/path/to/Assembly-CSharp.dll"]
    [Call warm_index typeIndex=true memberIndex=true maxSeconds=30]

    Loaded Assembly-CSharp.dll (8.4 MB, 2,847 types).
    Index warm-up complete: 2,847 types, 41,320 members indexed in 18.4 s.

User: Now find all methods named Update

AI: [Call search_types keyword="Update"]  ← instant, uses cached index

    ...
```

## Important Notes

1. **Path format**: Use absolute paths for reliability
2. **Type names**: Prefer full namespace names
3. **Large assemblies**: Use limit parameter to control result count
4. **Dependency resolution**: Use searchPaths to specify dependency directories
5. **Backup before modifying**: Always backup original file before making changes
6. **Aliases persist across sessions**: Use `register_assembly_alias` + `instance_restore_persisted` to avoid re-loading assemblies every session

## Next Steps

- [Tools Reference](tools-reference.md) - View detailed parameters for all tools
- [Configuration](configuration.md) - Learn more configuration options
