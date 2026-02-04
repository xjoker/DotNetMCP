# Tools Reference

English | [中文](../zh/tools-reference.md)

This document details all MCP tools with parameters and usage, including AI conversation examples.

---

## Assembly Management Tools

### load_assembly

Load a .NET assembly for analysis.

**Use Cases:**
- Start analyzing a new DLL/EXE file
- Examine assembly internals

**AI Conversation Examples:**
> "Load /path/to/MyApp.dll"
>
> "Analyze this assembly: C:\Projects\MyLib.dll"
>
> "Open assembly ./bin/Debug/net8.0/App.dll and tell me what types it has"
>
> "Load MyApp.dll with dependency directory /path/to/libs"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | string | Yes | Assembly file path (.dll or .exe) |
| `searchPaths` | string[] | No | Dependency search directories for resolving references |
| `backendId` | string | No | Target backend ID |

**Response Example:**
```json
{
  "success": true,
  "mvid": "12345678-1234-1234-1234-123456789abc",
  "name": "MyAssembly",
  "backend": "local"
}
```

**Notes:**
- Path should be absolute or relative to current working directory
- Use searchPaths for assemblies with external dependencies

---

### list_assemblies

List loaded assemblies.

**Use Cases:**
- View all assemblies loaded in current session
- Get assembly MVID for subsequent operations

**AI Conversation Examples:**
> "List loaded assemblies"
>
> "What assemblies are loaded?"
>
> "Show all loaded DLLs"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `backendId` | string | No | Target backend ID |

**Response Example:**
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

Unload an assembly.

**Use Cases:**
- Release assemblies no longer needed
- Clean up session resources

**AI Conversation Examples:**
> "Unload MyApp assembly"
>
> "Don't need MyLib.dll anymore, unload it"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `mvid` | string | Yes | Assembly MVID |
| `backendId` | string | No | Target backend ID |

---

## Search Tools

### search_types

Search types by keyword.

**Use Cases:**
- Find specific types in assembly
- Explore assembly structure
- Filter types by namespace

**AI Conversation Examples:**
> "Search for types containing Service"
>
> "Find all Controller classes"
>
> "List all types in MyApp.Services namespace"
>
> "What types are there?" (empty keyword lists all)

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `keyword` | string | Yes | Search keyword (empty matches all) |
| `namespaceFilter` | string | No | Namespace filter |
| `limit` | int | No | Result limit (default 50) |
| `mvid` | string | No | Specific assembly MVID |

**Response Example:**
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

Search string literals.

**Use Cases:**
- Find hardcoded passwords, keys
- Search URLs, configuration strings
- Analyze text content in program

**AI Conversation Examples:**
> "Search for strings containing password"
>
> "Find all URL strings"
>
> "Are there any hardcoded API keys in this assembly?"
>
> "Search for strings starting with http://"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `query` | string | Yes | Search query |
| `mode` | string | No | Search mode: contains, exact, startswith (default contains) |
| `limit` | int | No | Result limit (default 50) |
| `mvid` | string | No | Specific assembly MVID |

**Response Example:**
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

## Analysis Tools

### decompile_type

Decompile type to C# or IL.

**Use Cases:**
- View complete type implementation
- Analyze class structure and methods
- Understand code logic

**AI Conversation Examples:**
> "Decompile the MyApp.Services.UserService class"
>
> "Show me UserService source code"
>
> "Decompile Program class to IL code"
>
> "Show MyClass implementation"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `typeName` | string | Yes | Full type name |
| `language` | string | No | Output language: csharp, il (default csharp) |
| `mvid` | string | No | Specific assembly MVID |

**Response Example:**
```json
{
  "success": true,
  "typeName": "MyNamespace.MyClass",
  "code": "public class MyClass { ... }"
}
```

---

### decompile_method

Decompile a method.

**Use Cases:**
- View specific method implementation
- Analyze method logic
- View IL instructions

**AI Conversation Examples:**
> "Decompile UserService.GetUser method"
>
> "Show me the code for Login method"
>
> "Display IL code for Main method"
>
> "How is DoWork method implemented?"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `typeName` | string | Yes | Full type name |
| `methodName` | string | Yes | Method name |
| `language` | string | No | Output language: csharp, il (default csharp) |
| `mvid` | string | No | Specific assembly MVID |

**Response Example:**
```json
{
  "success": true,
  "methodName": "GetUser",
  "code": "public User GetUser(int id) { ... }"
}
```

---

### find_type_references

Find type references.

**Use Cases:**
- Understand where a type is used
- Analyze dependencies
- Evaluate modification impact

**AI Conversation Examples:**
> "Find where UserService class is used"
>
> "Who references ILogger interface?"
>
> "Where is User type being used?"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `typeName` | string | Yes | Full type name |
| `limit` | int | No | Result limit (default 50) |
| `mvid` | string | No | Specific assembly MVID |

**Response Example:**
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

Find method calls.

**Use Cases:**
- Find all places calling a method
- Analyze method usage
- Trace code execution paths

**AI Conversation Examples:**
> "Who calls ValidateToken method?"
>
> "Find all places calling SaveUser"
>
> "Where is GetData method used?"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `typeName` | string | Yes | Full type name |
| `methodName` | string | Yes | Method name |
| `limit` | int | No | Result limit (default 50) |
| `mvid` | string | No | Specific assembly MVID |

**Response Example:**
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

Build call graph.

**Use Cases:**
- Analyze method call chains
- Understand code execution flow
- Visualize method dependencies

**AI Conversation Examples:**
> "Analyze Main method's call graph"
>
> "What methods does ProcessOrder call?"
>
> "Show Initialize method call chain with depth 5"
>
> "Who calls Login method?" (callers direction)

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `typeName` | string | Yes | Full type name |
| `methodName` | string | Yes | Method name |
| `direction` | string | No | Direction: callees, callers (default callees) |
| `maxDepth` | int | No | Maximum depth (default 3) |
| `maxNodes` | int | No | Maximum nodes (default 100) |
| `mvid` | string | No | Specific assembly MVID |

**Response Example:**
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

Build control flow graph.

**Use Cases:**
- Analyze method execution paths
- Understand conditional branches and loops
- Visualize complex method structure

**AI Conversation Examples:**
> "Show control flow graph for ProcessOrder method"
>
> "Analyze execution paths in ValidateInput method"
>
> "Generate CFG for ComplexMethod with IL instructions"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `typeName` | string | Yes | Full type name |
| `methodName` | string | Yes | Method name |
| `includeIL` | bool | No | Include IL instructions (default false) |
| `mvid` | string | No | Specific assembly MVID |

**Response Example:**
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

## Modification Tools

### inject_at_entry

Inject code at method entry.

**Use Cases:**
- Add logging
- Insert debug code
- Implement method interception

**AI Conversation Examples:**
> "Add logging at Login method entry"
>
> "Output 'GetUser called' at the start of GetUser method"
>
> "Add entry tracing to ProcessOrder method"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `methodFullName` | string | Yes | Full method name (Type.Method) |
| `instructions` | object[] | Yes | IL instruction list |
| `mvid` | string | No | Specific assembly MVID |

**Instruction Format Example:**
```json
[
  {"opCode": "ldstr", "stringValue": "Method called"},
  {"opCode": "call", "stringValue": "System.Console::WriteLine"}
]
```

---

### replace_method_body

Replace method body.

**Use Cases:**
- Modify method implementation
- Bypass validation logic
- Fix problematic code

**AI Conversation Examples:**
> "Change IsLicenseValid method to always return true"
>
> "Make CheckPermission method just return true"
>
> "Modify GetVersion method to return '2.0'"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `methodFullName` | string | Yes | Full method name |
| `instructions` | object[] | Yes | New IL instruction list |
| `mvid` | string | No | Specific assembly MVID |

**Example (return true):**
```json
[
  {"opCode": "ldc.i4.1"},
  {"opCode": "ret"}
]
```

---

### add_type

Add a new type.

**Use Cases:**
- Add new class to assembly
- Create helper types
- Inject custom code

**AI Conversation Examples:**
> "Add a new class MyApp.Helpers.Logger"
>
> "Create a static class named DebugHelper"
>
> "Add a class that implements IDisposable"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `typeName` | string | Yes | New type full name |
| `kind` | string | No | Type kind: class, interface, struct (default class) |
| `baseType` | string | No | Base type name |
| `mvid` | string | No | Specific assembly MVID |

---

### save_assembly

Save modified assembly.

**Use Cases:**
- Save all modifications
- Export modified assembly
- Create modified copy

**AI Conversation Examples:**
> "Save modified assembly to /path/to/Modified.dll"
>
> "Save changes to output.dll"
>
> "Export modified assembly"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `outputPath` | string | Yes | Output file path |
| `mvid` | string | No | Specific assembly MVID |

**Notes:**
- Ensure all modifications are complete before saving
- Recommended to backup original file first

---

## Backend Management Tools

### list_backends

List all backends.

**Use Cases:**
- View available analysis backends
- Check backend status

**AI Conversation Examples:**
> "List all available backends"
>
> "What backends are there?"

---

### register_remote_backend

Register a remote backend.

**Use Cases:**
- Connect to remote analysis service
- Implement distributed analysis

**AI Conversation Examples:**
> "Register remote backend http://server:5000"
>
> "Add remote analysis service http://192.168.1.100:5000 named remote-1"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `url` | string | Yes | Remote backend URL |
| `name` | string | No | Backend name |

---

### unregister_backend

Unregister a backend.

**Use Cases:**
- Remove unused backend
- Clean up session

**AI Conversation Examples:**
> "Unregister remote-1 backend"
>
> "Remove remote backend"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `backendId` | string | Yes | Backend ID |

---

### set_default_backend

Set default backend.

**Use Cases:**
- Switch primary backend
- Specify default analysis service

**AI Conversation Examples:**
> "Set remote-1 as default backend"
>
> "Switch to local backend"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `backendId` | string | Yes | Backend ID |

---

### check_backend_health

Check backend health status.

**Use Cases:**
- Verify backend is working properly
- Diagnose connection issues

**AI Conversation Examples:**
> "Check backend health status"
>
> "Is remote-1 backend working?"
>
> "What's the status of all backends?"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `backendId` | string | No | Backend ID (empty to check all) |

---

## Next Steps

- [AI Usage Guide](ai-usage-guide.md) - More conversation examples and tips
- [Configuration](configuration.md) - Learn more configuration options
