"""
DotNet MCP Server - Tools Package

MCP tool implementations for .NET assembly analysis and modification.

Tools: 26 total
- Core (4): get_assembly_info, get_type_source, get_method_source, get_type_info
- Search (1): search
- XRefs (1): get_xrefs
- Graphs (2): build_call_graph, build_cfg
- Detection (1): detect
- Instance (4): list_instances, set_default_instance, remove_instance, clear_cache
- Modification (3): inject_code, replace_body, save_assembly
- Resources (4): list_resources, get_resource, set_resource, remove_resource
- Dependencies (1): get_dependencies
- Transaction (3): begin_transaction, commit_transaction, rollback_transaction
- Transfer (1): create_transfer_token
- Export (1): export
"""

from . import core
from . import search
from . import xrefs
from . import graphs
from . import detection
from . import instance
from . import modification
from . import resources
from . import dependencies
from . import transaction
from . import transfer
from . import export

__all__ = [
    "core",
    "search",
    "xrefs",
    "graphs",
    "detection",
    "instance",
    "modification",
    "resources",
    "dependencies",
    "transaction",
    "transfer",
    "export"
]


def register_all_tools(mcp):
    """Register all tools with the MCP server."""
    core.register_tools(mcp)
    search.register_tools(mcp)
    xrefs.register_tools(mcp)
    graphs.register_tools(mcp)
    detection.register_tools(mcp)
    instance.register_tools(mcp)
    modification.register_tools(mcp)
    resources.register_tools(mcp)
    dependencies.register_tools(mcp)
    transaction.register_tools(mcp)
    transfer.register_tools(mcp)
    export.register_tools(mcp)
