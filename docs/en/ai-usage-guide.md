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

## Important Notes

1. **Path format**: Use absolute paths for reliability
2. **Type names**: Prefer full namespace names
3. **Large assemblies**: Use limit parameter to control result count
4. **Dependency resolution**: Use searchPaths to specify dependency directories
5. **Backup before modifying**: Always backup original file before making changes

## Next Steps

- [Tools Reference](tools-reference.md) - View detailed parameters for all tools
- [Configuration](configuration.md) - Learn more configuration options
