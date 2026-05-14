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
| `preferOriginalSource` | bool | No | Prefer original source from PDB when available |
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

### get_type_outline

Get a metadata-based structural outline of a type without full decompilation.

**Use Cases:**
- Quick orientation on type structure
- List members without reading full source code
- Faster than decompile_type for large classes

**AI Conversation Examples:**
> "Show me the outline of UserService class"
>
> "What members does MyClass have?"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `typeName` | string | Yes | Full type name |
| `mvid` | string | No | Specific assembly MVID |

---

### plan_chunking

Plan line-range chunks for a type or method's decompiled source.

**Use Cases:**
- Break large source code into LLM-friendly chunks
- Plan paged reading of big classes

**AI Conversation Examples:**
> "Plan chunks for the large DatabaseService class"
>
> "How should I page through this 500-line class?"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `typeName` | string | Yes | Full type name |
| `methodName` | string | No | Method name (chunks only that method) |
| `targetChunkSize` | int | No | Target chars per chunk (default 6000) |
| `overlap` | int | No | Overlapping lines between chunks (default 2) |
| `mvid` | string | No | Specific assembly MVID |

---

### compare_assemblies

Compare two loaded assemblies to find structural differences.

**Use Cases:**
- Diff two versions of the same assembly
- Find what changed between builds
- Track modifications after patching

**AI Conversation Examples:**
> "Compare the two loaded assemblies to see what changed"
>
> "What types were added in the new version?"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `leftMvid` | string | Yes | MVID of original assembly |
| `rightMvid` | string | Yes | MVID of modified assembly |
| `namespaceFilter` | string | No | Filter by namespace prefix |
| `includeUnchanged` | bool | No | Include unchanged types (default false) |

---

### batch_decompile

Decompile multiple types or methods in a single call with a character budget.

**Use Cases:**
- Decompile several related classes at once
- Efficient batch analysis
- Reduce MCP round trips

**AI Conversation Examples:**
> "Decompile UserService, OrderService, and PaymentService together"
>
> "Batch decompile all the controller classes"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `memberKeys` | string[] | Yes | Array of member keys (TypeName or TypeName::MethodName) |
| `maxTotalChars` | int | No | Maximum total characters (default 200000) |
| `mvid` | string | No | Specific assembly MVID |

---

### get_dependency_graph

Build a dependency graph for a loaded assembly at three granularities. Returns node/edge statistics and a Mermaid diagram string for visualization.

**Use Cases:**
- Understand which external assemblies are referenced
- Analyze inter-namespace coupling
- Visualize a type's inheritance and reference relationships

**AI Conversation Examples:**
> "Show the dependency graph for this assembly"
>
> "Visualize type dependencies for MyApp.Services.UserService using Mermaid"
>
> "Analyze namespace coupling"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `level` | string | No | Granularity: assembly (default), namespace, or type |
| `rootType` | string | No | Required when level=type; full type name (e.g. `MyNamespace.MyClass`) |
| `maxDepth` | int | No | Max traversal depth for level=type (default 3, max 10) |
| `mvid` | string | No | Specific assembly MVID |
| `backendId` | string | No | Target backend ID |

**Response Fields:**
- `level` - granularity used
- `rootId` - root node ID
- `totalNodes` / `externalNodes` / `totalEdges` - graph statistics
- `mermaid` - Mermaid diagram string

---

### detect_design_patterns

Detect design patterns in a loaded assembly. Supports Singleton, Factory, AbstractFactory, Observer, Builder, Strategy, and Decorator.

**Use Cases:**
- Understand code architecture style at a glance
- Identify known patterns during reverse engineering to aid comprehension

**AI Conversation Examples:**
> "What design patterns are used in this assembly?"
>
> "Does UserService implement the Singleton pattern?"
>
> "Scan the entire assembly for design patterns"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `typeName` | string | No | Specific type to analyze; omit to scan entire assembly |
| `mvid` | string | No | Specific assembly MVID |
| `backendId` | string | No | Target backend ID |

**Response Fields:**
- `totalCount` - total patterns detected
- `summary` - text summary
- `patterns[]` - each result contains patternType, typeName, confidence, evidence[], relatedTypes[]

---

### enhanced_search

Full-featured assembly search with advanced query syntax support.

**Use Cases:**
- Complex multi-term queries with include/exclude logic
- Locate members by exact Metadata Token
- Unified search across types, members, and literals

**AI Conversation Examples:**
> "Search for types containing Auth but not Test: +Auth -Test"
>
> "Find all methods starting with Get using regex: /^Get/"
>
> "Search for the literal string 'https://api.example.com'"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `query` | string | Yes | Query string with optional advanced syntax |
| `mvid` | string | No | Specific assembly MVID |
| `mode` | string | No | Search mode: auto (default), type, member, method, field, property, event, literal, token |
| `namespaceFilter` | string | No | Filter by namespace prefix |
| `limit` | int | No | Max results (default 100, max 1000) |
| `backendId` | string | No | Target backend ID |

**Query Syntax:**
- `keyword` — plain keyword search (case-insensitive)
- `+include -exclude` — include/exclude filtering
- `=exact` — exact match
- `~fuzzy` — fuzzy match
- `/regex/` — regular expression
- `0xToken` — Metadata Token lookup

**Response Fields:**
- `items[]` - results with id, name, fullName, kind, declaringType, namespace, value, relevance
- `totalCount` / `hasMore` / `durationMs` / `mode`

---

### find_base_types

Find all base types and interfaces in a type's inheritance chain.

**Use Cases:**
- Understand the full inheritance hierarchy
- Identify all interfaces a type implements
- Inspect external base types from referenced assemblies

**AI Conversation Examples:**
> "What is the base class chain for UserService?"
>
> "What interfaces does MyClass implement?"
>
> "Find all base types of OrderProcessor"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `typeName` | string | Yes | Full type name (e.g. `MyNamespace.MyClass`) |
| `includeInterfaces` | bool | No | Include interfaces in result (default true) |
| `mvid` | string | No | Specific assembly MVID |
| `backendId` | string | No | Target backend ID |

**Response Fields:**
- `types[]` - each entry contains id, fullName, namespace, kind, isExternal
- `totalCount`

---

### find_derived_types

Find all types that inherit from (or implement) a given type in the current module.

**Use Cases:**
- Find all subclasses
- Analyze polymorphism
- Discover all indirect implementations of an interface

**AI Conversation Examples:**
> "What classes inherit from BaseController?"
>
> "Find all implementations of IRepository (including indirect)"
>
> "Show only direct subclasses of Animal"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `typeName` | string | Yes | Full name of base type or interface |
| `directOnly` | bool | No | Return only direct subclasses (default false = full recursive hierarchy) |
| `mvid` | string | No | Specific assembly MVID |
| `backendId` | string | No | Target backend ID |

**Response Fields:**
- `types[]` - each entry contains id, fullName, namespace, kind, isExternal
- `totalCount`

---

### get_implementations

Find all types that directly implement the specified interface.

**Use Cases:**
- Quickly locate direct implementors of an interface
- Combine with find_derived_types for indirect implementations

**AI Conversation Examples:**
> "What types implement IUserRepository?"
>
> "Find all direct implementations of IService"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `interfaceTypeName` | string | Yes | Full interface name (e.g. `MyNamespace.IService`) |
| `mvid` | string | No | Specific assembly MVID |
| `backendId` | string | No | Target backend ID |

**Response Fields:**
- `types[]` - each entry contains id, fullName, namespace, kind, isExternal
- `totalCount`

---

### get_overrides

Find all override implementations of a virtual or abstract method across derived types.

**Use Cases:**
- Find all entry points through a virtual dispatch
- Analyze polymorphic method implementations

**AI Conversation Examples:**
> "What overrides exist for the Execute method?"
>
> "Show all implementations of BaseHandler.Handle in derived classes"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `typeName` | string | Yes | Full name of the declaring type |
| `methodName` | string | Yes | Name of the virtual or abstract method |
| `mvid` | string | No | Specific assembly MVID |
| `backendId` | string | No | Target backend ID |

**Response Fields:**
- `methods[]` - each entry contains id, typeFullName, methodName, signature
- `totalCount`

---

### get_overloads

Find all overloads of a method within the same type.

**Use Cases:**
- Disambiguate an overloaded method before calling decompile_method
- Understand all call signatures for a method

**AI Conversation Examples:**
> "What overloads does the Parse method have?"
>
> "List all overloads of UserService.GetUser"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `typeName` | string | Yes | Full type name |
| `methodName` | string | Yes | Method name |
| `mvid` | string | No | Specific assembly MVID |
| `backendId` | string | No | Target backend ID |

**Response Fields:**
- `methods[]` - each entry contains id, typeFullName, methodName, signature
- `totalCount`

---

### detect_obfuscation

Detect if an assembly is obfuscated, identify the obfuscator, and get a score from 0 to 100.

**Use Cases:**
- Determine whether de-obfuscation is needed before reverse engineering
- Identify the obfuscator type to choose the right de-obfuscation tool

**AI Conversation Examples:**
> "Is this assembly obfuscated?"
>
> "Check obfuscation and tell me what obfuscator was used"
>
> "Analyze the obfuscation level of this assembly"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `mvid` | string | No | Specific assembly MVID |
| `backendId` | string | No | Target backend ID |

**Response Fields:**
- `isObfuscated` - whether the assembly is obfuscated
- `obfuscationScore` - score 0-100
- `confidence` - confidence level (Low/Medium/High)
- `detectedObfuscators[]` - names of identified obfuscators
- `topIndicators[]` - top 10 indicators with category, severity, description, location
- `stats` - statistics including type/method/field counts, invalid names, short names, control flow flattening, proxy methods

---

### warm_index

Pre-build type and member indexes for faster subsequent queries.

**Use Cases:**
- Pre-warm indexes before heavy analysis on large assemblies
- Reduce first-query latency in batch analysis workflows
- Indexes are otherwise built on-demand at first access

**AI Conversation Examples:**
> "Pre-warm the index for this assembly before I start analysis"
>
> "Build the type index now so queries are faster"
>
> "Warm the index with a 30-second budget"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `mvid` | string | No | MVID or alias of the assembly. Omit to use default. |
| `typeIndex` | bool | No | Build type index (default true) |
| `memberIndex` | bool | No | Build member index (default true) |
| `maxSeconds` | int | No | Soft time budget in seconds. If exceeded, member index building is skipped. |
| `backendId` | string | No | Target backend ID |

**Response Fields:**
- `typeIndexBuilt` - whether the type index was built
- `memberIndexBuilt` - whether the member index was built
- `typeCount` - number of types indexed
- `memberCount` - number of members indexed
- `elapsedMs` - elapsed time in milliseconds
- `maxSecondsExceeded` - whether the soft time budget was exceeded

---

## Assembly Management Tools (Extended)

### detect_unity_assembly

Detect Assembly-CSharp.dll in a Unity game directory. Supports Windows, macOS, and Linux Unity directory layouts.

**Use Cases:**
- Automatically locate the main assembly when reverse engineering Unity games
- Use when the exact DLL path is unknown

**AI Conversation Examples:**
> "Find the Unity assembly in /path/to/MyGame"
>
> "I have a Unity game — help me find Assembly-CSharp.dll"
>
> "What managed assemblies are in this game directory?"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `gameRootPath` | string | Yes | Path to the Unity game root directory or .app bundle |

**Response Fields:**
- `assemblyCSharpPath` - full path to Assembly-CSharp.dll
- `managedDirectory` - path to the Managed directory
- `gameName` - game name
- `platform` - detected platform (Windows/macOS/Linux)
- `unityVersion` - Unity version (if readable)
- `managedAssemblies[]` - list of all managed DLL paths

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
- Output path is restricted to the source assembly's directory

---

### generate_patch_skeleton

Generate a Harmony patch skeleton for a method.

**Use Cases:**
- Create Harmony patch templates for game modding
- Generate Prefix/Postfix/Transpiler/Finalizer patches
- Unity, RimWorld, and other game modding workflows

**AI Conversation Examples:**
> "Generate a Harmony prefix patch for PlayerController.Update"
>
> "Create a postfix patch for GameManager.SaveGame"
>
> "Generate all patch types for the Login method"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `typeName` | string | Yes | Full type name |
| `methodName` | string | Yes | Method name (use "Name(Type1,Type2)" for overloads) |
| `patchKinds` | string | No | Comma-separated: Prefix, Postfix, Transpiler, Finalizer (default "Prefix,Postfix") |
| `mvid` | string | No | Specific assembly MVID |

---

### replace_method_body_with_csharp

Replace a method body using C# source code instead of raw IL instructions.

**Use Cases:**
- Patch method logic without writing IL by hand
- Stub out methods with simple return values using readable C#
- Fix or override method implementations in existing assemblies

**AI Conversation Examples:**
> "Replace GetVersion to return '2.0' using C#"
>
> "Make IsLicenseValid always return true, write it as C#"
>
> "Replace the ValidateInput body with: if (input == null) return false; return input.Length > 0;"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `methodFullName` | string | Yes | Full method name, e.g. `"MyNamespace.MyClass::MyMethod"` or `"MyNamespace.MyClass.MyMethod"` |
| `csharpBody` | string | Yes | C# method body (without the signature). Example: `"return x + 1;"` |
| `mvid` | string | No | Assembly MVID or alias. Omit to use default. |
| `usings` | string[] | No | Extra using namespaces (defaults to System, System.Collections.Generic, System.Linq, System.Text) |
| `allowUnsafe` | bool | No | Allow unsafe C# code in the snippet (default false) |
| `backendId` | string | No | Target backend ID |

**Response Fields (success):**
- `success` - true
- `message` - confirmation with instruction count
- `instructionsReplaced` - number of IL instructions in the replaced body

**Response Fields (failure):**
- `success` - false
- `error` - error message
- `diagnostics[]` - Roslyn compilation diagnostics in format `[Severity] ErrorId (line N): message`

**Notes:**
- The body is compiled with Roslyn using the target method's parameter names and return type
- Call `save_assembly` after replacing to persist changes to disk

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
| `id` | string | Yes | Unique backend identifier |
| `name` | string | Yes | Display name for the backend |
| `endpoint` | string | Yes | HTTP endpoint URL |
| `apiKey` | string | No | API key for authentication |
| `timeoutSeconds` | int | No | Request timeout in seconds (default 30) |

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

## Assembly Alias Management Tools

Aliases let you register a short, human-readable name for a loaded assembly's MVID. Once registered, all tools that accept a `mvid` parameter can use the alias instead of the full 32-character GUID. Aliases are persisted to disk (`~/.local/share/dotnet-mcp/aliases.json` on Linux, `~/Library/Application Support/dotnet-mcp/aliases.json` on macOS, `%LOCALAPPDATA%\dotnet-mcp\aliases.json` on Windows) and can be restored across sessions with `instance_restore_persisted`.

**Alias rules:** 1–32 characters, `[A-Za-z0-9_-]`, not all-digits, not reserved words (`default`, `local`, `null`).

---

### register_assembly_alias

Register a short alias for a loaded assembly MVID.

**Use Cases:**
- Give the assembly a memorable name like `"main"` instead of a GUID
- Enable cross-session references by combining with `instance_restore_persisted`

**AI Conversation Examples:**
> "Register the current assembly as alias 'main'"
>
> "Alias this assembly as 'target'"
>
> "Register mvid abc123... as 'v2', overwrite if exists"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `alias` | string | Yes | Short alias (1–32 chars, `[A-Za-z0-9_-]`, not reserved) |
| `mvid` | string | No | Assembly MVID to bind. Omit to use the current default assembly. |
| `overwrite` | bool | No | If true, overwrite an existing alias with the same name (default false) |
| `backendId` | string | No | Target backend ID |

**Response Fields:**
- `alias` - the registered alias name
- `mvid` - the MVID it was bound to

---

### unregister_assembly_alias

Remove a previously registered assembly alias.

**Use Cases:**
- Clean up stale alias names
- Release an alias so it can be reused

**AI Conversation Examples:**
> "Remove alias 'main'"
>
> "Unregister the 'target' alias"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `alias` | string | Yes | Alias to remove |
| `backendId` | string | No | Target backend ID |

**Notes:**
- The underlying assembly remains loaded; only the alias mapping is deleted.

---

### list_assembly_aliases

List all registered assembly aliases for the current backend.

**Use Cases:**
- See what aliases are currently active
- Verify alias → MVID mappings before analysis

**AI Conversation Examples:**
> "List all assembly aliases"
>
> "What aliases are registered?"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `backendId` | string | No | Target backend ID |

**Response Fields:**
- `aliases[]` - array of `{ alias, mvid }` entries

---

### instance_restore_persisted

Reload assemblies from persisted alias entries saved to disk from a previous session.

**Use Cases:**
- Resume work across sessions without re-loading assemblies manually
- Restore a known workspace from a previous conversation

**AI Conversation Examples:**
> "Restore my previous session's assemblies"
>
> "Load assemblies from persisted aliases"

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `backendId` | string | No | Target backend ID |

**Response Fields:**
- `restoredCount` - number of successfully restored assemblies

**Notes:**
- Failed entries (missing files, invalid paths) are automatically removed from persistence.

---

## Next Steps

- [AI Usage Guide](ai-usage-guide.md) - More conversation examples and tips
- [Configuration](configuration.md) - Learn more configuration options
