"""
Analysis Tools - MCP tools for .NET assembly analysis

Tools for reading assembly metadata, decompiling code, searching, and cross-references.
"""

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
        return await make_request(
            instance, "GET", 
            f"/analysis/type/{type_name}/source",
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
        return await make_request(
            instance, "GET",
            f"/analysis/type/{type_name}/method/{method_name}",
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
        return await make_request(instance, "GET", f"/analysis/type/{type_name}/info")

    @mcp.tool("search_types_by_keyword")
    async def search_types_by_keyword(
        keyword: str,
        namespace_filter: str = None,
        limit: int = 50,
        cursor: str = None,
        instance_name: str = None
    ) -> dict:
        """
        Search types by keyword in name.
        
        Args:
            keyword: Search keyword (supports wildcards: * ?)
            namespace_filter: Optional namespace prefix filter
            limit: Max results (default 50, max 500)
            cursor: Pagination cursor
            instance_name: Optional instance name.
        
        Returns:
            Paginated list of matching types with MemberIds.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        params = {"keyword": keyword, "limit": limit}
        if namespace_filter:
            params["namespace"] = namespace_filter
        if cursor:
            params["cursor"] = cursor
        return await make_request(instance, "GET", "/analysis/search/types", params=params)

    @mcp.tool("search_string_literals")
    async def search_string_literals(
        query: str,
        match_mode: str = "contains",
        limit: int = 50,
        cursor: str = None,
        instance_name: str = None
    ) -> dict:
        """
        Search for string literals in the assembly.
        
        Args:
            query: Search string
            match_mode: "contains" | "exact" | "startswith" | "regex"
            limit: Max results (default 50, max 500)
            cursor: Pagination cursor
            instance_name: Optional instance name.
        
        Returns:
            Paginated list of string occurrences with LocationIds.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        params = {"query": query, "mode": match_mode, "limit": limit}
        if cursor:
            params["cursor"] = cursor
        return await make_request(instance, "GET", "/analysis/search/strings", params=params)

    @mcp.tool("get_xrefs_to_type")
    async def get_xrefs_to_type(
        type_name: str,
        limit: int = 50,
        cursor: str = None,
        instance_name: str = None
    ) -> dict:
        """
        Find all references to a type.
        
        Args:
            type_name: Fully qualified type name
            limit: Max results
            cursor: Pagination cursor
            instance_name: Optional instance name.
        
        Returns:
            Paginated list of usage locations with context.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        params = {"limit": limit}
        if cursor:
            params["cursor"] = cursor
        return await make_request(
            instance, "GET", f"/analysis/xrefs/type/{type_name}", params=params
        )

    @mcp.tool("get_xrefs_to_method")
    async def get_xrefs_to_method(
        member_id: str,
        limit: int = 50,
        cursor: str = None,
        instance_name: str = None
    ) -> dict:
        """
        Find all references to a method.
        
        Args:
            member_id: MemberId of the method
            limit: Max results
            cursor: Pagination cursor
            instance_name: Optional instance name.
        
        Returns:
            Paginated list of call sites with context.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        params = {"limit": limit}
        if cursor:
            params["cursor"] = cursor
        return await make_request(
            instance, "GET", f"/analysis/xrefs/method/{member_id}", params=params
        )

    @mcp.tool("build_call_graph")
    async def build_call_graph(
        member_id: str,
        direction: str = "callees",
        max_depth: int = 3,
        max_nodes: int = 100,
        instance_name: str = None
    ) -> dict:
        """
        Build call graph starting from a method.
        
        Args:
            member_id: Entry method MemberId
            direction: "callees" | "callers" | "both"
            max_depth: Maximum graph depth (default 3)
            max_nodes: Maximum nodes (default 100)
            instance_name: Optional instance name.
        
        Returns:
            Graph with nodes and edges.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        params = {
            "direction": direction,
            "max_depth": max_depth,
            "max_nodes": max_nodes
        }
        return await make_request(
            instance, "GET", f"/analysis/callgraph/{member_id}", params=params
        )
