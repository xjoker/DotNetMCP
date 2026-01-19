"""
MCP Prompts - Pre-defined prompt templates for AI guidance

Prompts help AI follow best practices when using the tools.
"""

from fastmcp import FastMCP


def register_prompts(mcp: FastMCP):
    """Register prompts with the MCP server."""

    @mcp.prompt("status-check")
    def status_check_prompt() -> str:
        """Guide AI to check status before resource-intensive operations."""
        return """**Before performing resource-intensive operations, check status first.**

Call `get_analysis_status()` and interpret the results:

## Response Fields and Decision Rules

### 1. Index Status
| Field | Threshold | Action |
|-------|-----------|--------|
| index.percentage < 20% | Low index | Use metadata search only |
| index.percentage > 50% | Good index | Full-text search available |
| index.percentage = 100% | Complete | All features available |

### 2. Memory Status
| Field | Threshold | Action |
|-------|-----------|--------|
| memory.usage_percentage > 85% | High | Reduce batch size to 5 |
| memory.usage_percentage > 95% | Critical | Avoid batch operations |

### 3. Session Status
| Field | Threshold | Action |
|-------|-----------|--------|
| sessions.modify_count > 3 | Busy | Wait or notify user |

## Example Workflow
1. Call `get_analysis_status()`
2. Check index percentage - if < 20%, avoid code search
3. Check memory - if > 85%, reduce batch sizes
4. Proceed with appropriate tool selection
"""

    @mcp.prompt("analyze-type")
    def analyze_type_prompt() -> str:
        """Standard type analysis workflow."""
        return """**Standard Type Analysis Workflow**

Follow this workflow for thorough type analysis:

## Step 1: Get Type Overview
```
get_type_info(type_name="TargetClass")
```
Understand inheritance, interfaces, and member list.

## Step 2: Get Source Code
```
get_type_source(type_name="TargetClass", language="csharp")
```
Read the decompiled implementation.

## Step 3: Analyze Key Methods
```
get_method_by_name(type_name="TargetClass", method_name="ImportantMethod")
```
Focus on interesting methods.

## Step 4: Find Usages
```
get_xrefs_to_type(type_name="TargetClass")
```
Understand how the type is used.

## Step 5: Build Call Graph (if needed)
```
build_call_graph(member_id="<method_id>", direction="both", max_depth=2)
```
Trace execution flow.

## Tips
- Start with `get_type_info` for structure overview
- Use `search_string_literals` to find interesting entry points
- Use batch operations for multiple related types
"""

    @mcp.prompt("patch-method")
    def patch_method_prompt() -> str:
        """Standard method modification workflow."""
        return """**Method Modification Workflow**

Follow this workflow for safe method modification:

## Step 1: Analyze Current Implementation
```
get_method_by_name(type_name="TargetClass", method_name="TargetMethod")
```
Understand what the method does.

## Step 2: Check References
```
get_xrefs_to_method(member_id="<method_id>")
```
Understand impact of modification.

## Step 3: Begin Session
```
begin_modify_session()
```
Start a modification session.

## Step 4: Make Modification
Choose one:
- `replace_method_body()` - Full replacement
- `inject_il()` - Add code at specific point
- `wrap_method()` - Add before/after hooks

## Step 5: Review Before Commit
The modification tools return validation results.
Check for:
- IL stack imbalance
- Invalid references
- Type mismatches

## Step 6: Commit or Rollback
```
# If validation passed:
commit_session(session_id="...", output_path="Modified.dll")

# If issues found:
rollback_session(session_id="...")
```

## Safety Tips
- Always backup before committing
- Test modifications on copies first
- Check the ID mapping table after commit
"""

    @mcp.prompt("find-vulnerability")
    def find_vulnerability_prompt() -> str:
        """Security audit workflow."""
        return """**Security Audit Workflow**

Follow this workflow for security analysis:

## Step 1: Search Sensitive Strings
```
search_string_literals(query="password", match_mode="contains")
search_string_literals(query="secret", match_mode="contains")
search_string_literals(query="api_key", match_mode="contains")
search_string_literals(query="http://", match_mode="startswith")
```

## Step 2: Find Crypto Usage
```
search_types_by_keyword(keyword="*Crypto*")
search_types_by_keyword(keyword="*Encrypt*")
search_method_by_name(method_name="Decrypt")
```

## Step 3: Analyze Authentication
```
search_types_by_keyword(keyword="*Auth*")
search_types_by_keyword(keyword="*Login*")
```

## Step 4: Trace Data Flow
For interesting findings, trace the call graph:
```
build_call_graph(member_id="<suspicious_method>", direction="callers")
```

## Step 5: Check Common Vulnerabilities
- Hardcoded credentials (string literals)
- Weak cryptography (DES, MD5, SHA1)
- SQL injection (string concatenation with SQL keywords)
- Insecure HTTP (http:// URLs)

## Reporting
Document findings with:
- MemberId for precise location
- Code snippet as evidence
- Severity assessment
"""
