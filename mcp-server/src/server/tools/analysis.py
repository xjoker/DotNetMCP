"""
Analysis Tools - MCP tools for .NET assembly analysis

Tools for reading assembly metadata, decompiling code, searching, and cross-references.
Updated to match the C# REST API endpoints.
"""

from urllib.parse import quote
from fastmcp import FastMCP

from ..instance_registry import InstanceRegistry
from ..config import make_request


def register_tools(mcp: FastMCP):
    """Register analysis tools with the MCP server."""

    @mcp.tool("get_assembly_info")
    async def get_assembly_info(instance_name: str = None) -> dict:
        """
        Get assembly information. Recommended as the first call.
        
        Returns assembly name, version, target framework, types count, etc.
        
        Args:
            instance_name: Optional instance name. Uses default if not specified.
        
        Returns:
            Assembly metadata including name, version, framework, statistics.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        return await make_request(instance, "GET", "/assembly/info")

    @mcp.tool("get_type_source")
    async def get_type_source(
        type_name: str,
        language: str = "csharp",
        instance_name: str = None
    ) -> dict:
        """
        Get decompiled source code of a type.
        
        Args:
            type_name: Fully qualified type name (e.g., "MyNamespace.MyClass")
            language: Output language: "csharp" | "il"
            instance_name: Optional instance name.
        
        Returns:
            Decompiled source code with line mappings.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        # URL encode the type name to handle special characters
        encoded_type = quote(type_name, safe='')
        return await make_request(
            instance, "GET", 
            f"/analysis/type/{encoded_type}/source",
            params={"language": language}
        )

    @mcp.tool("get_method_by_name")
    async def get_method_by_name(
        type_name: str,
        method_name: str,
        language: str = "csharp",
        instance_name: str = None
    ) -> dict:
        """
        Get decompiled source code of a specific method.
        
        Args:
            type_name: Fully qualified type name
            method_name: Method name (without parameters)
            language: Output language: "csharp" | "il"
            instance_name: Optional instance name.
        
        Returns:
            Method source code with signature and body.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        encoded_type = quote(type_name, safe='')
        encoded_method = quote(method_name, safe='')
        return await make_request(
            instance, "GET",
            f"/analysis/type/{encoded_type}/method/{encoded_method}",
            params={"language": language}
        )

    @mcp.tool("get_type_info")
    async def get_type_info(type_name: str, instance_name: str = None) -> dict:
        """
        Get type structure information (inheritance, interfaces, members).
        
        Args:
            type_name: Fully qualified type name
            instance_name: Optional instance name.
        
        Returns:
            Type metadata including base type, interfaces, methods, fields, properties.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        encoded_type = quote(type_name, safe='')
        return await make_request(instance, "GET", f"/analysis/type/{encoded_type}/info")

    @mcp.tool("search_types_by_keyword")
    async def search_types_by_keyword(
        keyword: str,
        namespace_filter: str = None,
        limit: int = 50,
        instance_name: str = None
    ) -> dict:
        """
        Search types by keyword in name.
        
        Args:
            keyword: Search keyword (supports wildcards: * ?)
            namespace_filter: Optional namespace prefix filter
            limit: Max results (default 50, max 500)
            instance_name: Optional instance name.
        
        Returns:
            List of matching types with metadata.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        params = {"keyword": keyword, "limit": limit}
        if namespace_filter:
            params["namespace"] = namespace_filter
        return await make_request(instance, "GET", "/analysis/search/types", params=params)

    @mcp.tool("search_string_literals")
    async def search_string_literals(
        query: str,
        match_mode: str = "contains",
        limit: int = 50,
        instance_name: str = None
    ) -> dict:
        """
        Search for string literals in the assembly.
        
        Args:
            query: Search string
            match_mode: "contains" | "exact" | "startswith"
            limit: Max results (default 50, max 500)
            instance_name: Optional instance name.
        
        Returns:
            List of string occurrences with location info.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        params = {"query": query, "mode": match_mode, "limit": limit}
        return await make_request(instance, "GET", "/analysis/search/strings", params=params)

    @mcp.tool("get_xrefs_to_type")
    async def get_xrefs_to_type(
        type_name: str,
        limit: int = 50,
        instance_name: str = None
    ) -> dict:
        """
        Find all references to a type.
        
        Args:
            type_name: Fully qualified type name
            limit: Max results (default 50)
            instance_name: Optional instance name.
        
        Returns:
            List of usage locations with context.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        encoded_type = quote(type_name, safe='')
        params = {"limit": limit}
        return await make_request(
            instance, "GET", f"/analysis/xrefs/type/{encoded_type}", params=params
        )

    @mcp.tool("get_xrefs_to_method")
    async def get_xrefs_to_method(
        type_name: str,
        method_name: str,
        limit: int = 50,
        instance_name: str = None
    ) -> dict:
        """
        Find all references (call sites) to a method.
        
        Args:
            type_name: Fully qualified type name containing the method
            method_name: Method name
            limit: Max results (default 50)
            instance_name: Optional instance name.
        
        Returns:
            List of call sites with context.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        encoded_type = quote(type_name, safe='')
        encoded_method = quote(method_name, safe='')
        params = {"limit": limit}
        return await make_request(
            instance, "GET", f"/analysis/xrefs/method/{encoded_type}/{encoded_method}", params=params
        )

    @mcp.tool("build_call_graph")
    async def build_call_graph(
        type_name: str,
        method_name: str,
        direction: str = "callees",
        max_depth: int = 3,
        max_nodes: int = 100,
        instance_name: str = None
    ) -> dict:
        """
        Build call graph starting from a method.
        
        Args:
            type_name: Fully qualified type name containing the method
            method_name: Entry method name
            direction: "callees" | "callers" | "both"
            max_depth: Maximum graph depth (default 3)
            max_nodes: Maximum nodes (default 100)
            instance_name: Optional instance name.
        
        Returns:
            Graph with nodes and edges showing call relationships.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        encoded_type = quote(type_name, safe='')
        encoded_method = quote(method_name, safe='')
        params = {
            "direction": direction,
            "max_depth": max_depth,
            "max_nodes": max_nodes
        }
        return await make_request(
            instance, "GET", f"/analysis/callgraph/{encoded_type}/{encoded_method}", params=params
        )
