"""
MCP Resources - Static resources AI can read for guidance

Resources provide documentation that AI can access to learn how to use the tools.
"""

from fastmcp import FastMCP


def register_resources(mcp: FastMCP):
    """Register resources with the MCP server."""

    @mcp.resource("dotnetmcp://usage-guide")
    def usage_guide() -> str:
        """Provide usage guide for the DotNet MCP server."""
        return """# DotNet MCP Server - Usage Guide

## Quick Start
1. **Always check status first**: Call `get_analysis_status()` before any operation
2. **Use appropriate searches**: Prefer type/method search over full-text
3. **Use batch operations**: For multiple items, use `batch_get_*` tools

## Decision Matrix
| Status Field | Threshold | Recommended Action |
|--------------|-----------|-------------------|
| index.percentage < 20% | Low | Use metadata search only |
| index.percentage > 50% | Good | Full-text search available |
| memory.usage > 85% | High | Reduce batch size to 5 |
| sessions.modify_count > 3 | Busy | Wait or notify user |

## Performance Expectations
| Operation | Expected Time | Notes |
|-----------|--------------|-------|
| get_type_info | <100ms | Metadata only |
| get_type_source | <500ms | Decompilation |
| search_types_by_keyword | <500ms | Indexed |
| search_string_literals | <1s | May scan |
| build_call_graph | 1-5s | Depends on depth |
| replace_method_body | 1-3s | Includes validation |

## Tool Categories

### Fast (Always Available)
- `get_assembly_info`
- `get_type_info`
- `list_instances`
- `get_analysis_status`

### Moderate (Check Status First)
- `get_type_source`
- `get_method_by_name`
- `search_types_by_keyword`
- `get_xrefs_to_*`
- `batch_get_*`

### Slow (Requires Resources)
- `build_call_graph`
- `replace_method_body`
- `inject_il`
- `commit_session`

## Prompts Available
- `status-check`: Best practices for status checking
- `analyze-type`: Standard type analysis workflow
- `patch-method`: Method modification workflow
- `find-vulnerability`: Security audit workflow
"""

    @mcp.resource("dotnetmcp://decision-matrix")
    def decision_matrix() -> str:
        """Provide decision matrix for tool selection."""
        return """# Tool Selection Decision Matrix

## By Task Type

### Finding Code
| Goal | Recommended Tool |
|------|------------------|
| Find by class name | `search_types_by_keyword` |
| Find by method name | `search_method_by_name` |
| Find by string content | `search_string_literals` |
| Find usages | `get_xrefs_to_*` |

### Understanding Code
| Goal | Recommended Tool |
|------|------------------|
| Type structure | `get_type_info` |
| Source code | `get_type_source` |
| Specific method | `get_method_by_name` |
| Execution flow | `build_call_graph` |

### Modifying Code
| Goal | Recommended Tool |
|------|------------------|
| Replace method | `replace_method_body` |
| Add logging | `inject_il` at start |
| Add validation | `inject_il` at start |
| Remove feature | `remove_member` or empty body |

## By Status

### When index.percentage < 20%
- ✅ `get_type_info` (metadata)
- ✅ `get_assembly_info`
- ❌ `search_string_literals` (slow)
- ❌ Full-text search

### When memory.usage > 85%
- ✅ Single item operations
- ⚠️ `batch_*` with limit=5
- ❌ `batch_*` with limit=20
- ❌ `build_call_graph` with depth>2

### When sessions.modify_count > 3
- ✅ Read-only operations
- ⚠️ New modify sessions
- Recommend: Notify user about active sessions
"""

    @mcp.resource("dotnetmcp://capabilities")
    def capabilities() -> str:
        """List current capabilities and loaded instances."""
        from .instance_registry import InstanceRegistry
        
        instances = InstanceRegistry.list_instances()
        default = InstanceRegistry.get_default_name()
        
        lines = ["# Current Capabilities\n"]
        lines.append(f"## Loaded Instances: {len(instances)}\n")
        
        for inst in instances:
            status_icon = "✅" if inst.status == "connected" else "❌"
            default_mark = " (default)" if inst.name == default else ""
            lines.append(f"- {status_icon} **{inst.name}**{default_mark}: {inst.url}")
        
        lines.append("\n## Available Features\n")
        lines.append("- Analysis: Search, Decompile, Cross-references, Call graphs")
        lines.append("- Modification: Method replacement, IL injection, Member operations")
        lines.append("- Batch: Up to 20 items per request")
        
        return "\n".join(lines)
