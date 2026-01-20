"""
Dependency Graph Tools - MCP tools for assembly and type dependency analysis

Tools for analyzing dependency relationships and generating Mermaid diagrams.
"""

from fastmcp import FastMCP

from ..instance_registry import InstanceRegistry
from ..config import make_request


def register_tools(mcp: FastMCP):
    """Register dependency graph tools with the MCP server."""

    @mcp.tool("get_assembly_dependencies")
    async def get_assembly_dependencies(
        include_system: bool = False,
        format: str = "json",
        mvid: str = None,
        instance_name: str = None
    ) -> dict:
        """
        Get assembly-level dependency graph.
        
        Args:
            include_system: Include System.* and Microsoft.* assemblies (default: False)
            format: Output format: "json" | "mermaid" (default: json)
            mvid: Optional assembly MVID
            instance_name: Optional instance name.
        
        Returns:
            Assembly dependencies with names, versions, and system/external flags.
            If format="mermaid", returns a Mermaid diagram string.
            
        Example:
            # Get dependencies as JSON
            get_assembly_dependencies()
            
            # Get Mermaid diagram
            get_assembly_dependencies(format="mermaid", include_system=True)
        """
        instance = InstanceRegistry.get_instance(instance_name)
        params = {
            "includeSystem": str(include_system).lower(),
            "format": format
        }
        if mvid:
            params["mvid"] = mvid
        return await make_request(instance, "GET", "/analysis/dependencies/assembly", params=params)

    @mcp.tool("get_type_dependencies")
    async def get_type_dependencies(
        type_name: str,
        max_depth: int = 3,
        format: str = "json",
        mvid: str = None,
        instance_name: str = None
    ) -> dict:
        """
        Get type-level dependency graph showing inheritance, interfaces, and field types.
        
        Args:
            type_name: Fully qualified type name (e.g., "MyApp.Models.User")
            max_depth: Maximum depth for dependency traversal (1-5, default: 3)
            format: Output format: "json" | "mermaid" (default: json)
            mvid: Optional assembly MVID
            instance_name: Optional instance name.
        
        Returns:
            Type dependencies with kind (Inheritance/Interface/Field/etc.) and external flags.
            If format="mermaid", returns a Mermaid diagram suitable for visualization.
            
        Example:
            # Get dependencies for a specific type
            get_type_dependencies("MyApp.Services.UserService")
            
            # Get Mermaid diagram with shallow depth
            get_type_dependencies("MyApp.Models.Order", max_depth=2, format="mermaid")
        """
        instance = InstanceRegistry.get_instance(instance_name)
        params = {
            "maxDepth": max_depth,
            "format": format
        }
        if mvid:
            params["mvid"] = mvid
        return await make_request(instance, "GET", f"/analysis/dependencies/type/{type_name}", params=params)
