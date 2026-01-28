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

    @mcp.tool("build_control_flow_graph")
    async def build_control_flow_graph(
        type_name: str,
        method_name: str,
        include_il: bool = False,
        instance_name: str = None
    ) -> dict:
        """
        Build control flow graph (CFG) for a method.

        Analyzes the IL instructions to identify basic blocks and control flow edges.
        Useful for understanding method structure, loops, and branching logic.

        Args:
            type_name: Fully qualified type name containing the method
            method_name: Method name to analyze
            include_il: Include IL instructions in each block (default False)
            instance_name: Optional instance name.

        Returns:
            CFG with basic blocks, edges, entry/exit points, and Mermaid diagram.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        encoded_type = quote(type_name, safe='')
        encoded_method = quote(method_name, safe='')
        params = {"include_il": str(include_il).lower()}
        return await make_request(
            instance, "GET", f"/analysis/cfg/{encoded_type}/{encoded_method}", params=params
        )

    @mcp.tool("build_dependency_graph")
    async def build_dependency_graph(
        level: str = "assembly",
        root_type: str = None,
        max_depth: int = 3,
        instance_name: str = None
    ) -> dict:
        """
        Build dependency graph at specified level.

        Analyzes dependencies between assemblies, namespaces, or types.
        Useful for understanding architecture and identifying coupling.

        Args:
            level: Analysis level - "assembly" | "namespace" | "type"
            root_type: For type level, optional root type to start from
            max_depth: Maximum depth for type-level analysis (default 3)
            instance_name: Optional instance name.

        Returns:
            Dependency graph with nodes, edges, statistics, and Mermaid diagram.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        params = {"level": level, "max_depth": max_depth}
        if root_type:
            params["root_type"] = root_type
        return await make_request(instance, "GET", "/analysis/dependencies", params=params)

    @mcp.tool("detect_design_patterns")
    async def detect_design_patterns(
        type_name: str = None,
        instance_name: str = None
    ) -> dict:
        """
        Detect common design patterns in the assembly.

        Identifies patterns like Singleton, Factory, Observer, Builder, Strategy, Decorator.
        Provides confidence level and evidence for each detection.

        Args:
            type_name: Optional specific type to analyze. Analyzes all types if not specified.
            instance_name: Optional instance name.

        Returns:
            List of detected patterns with type, confidence, and evidence.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        params = {}
        if type_name:
            params["type_name"] = type_name
        return await make_request(instance, "GET", "/analysis/patterns", params=params)

    @mcp.tool("detect_obfuscation")
    async def detect_obfuscation(instance_name: str = None) -> dict:
        """
        Detect if the assembly is obfuscated and identify obfuscation techniques.

        Analyzes multiple indicators:
        - Known obfuscator markers (Dotfuscator, ConfuserEx, etc.)
        - Invalid/random identifier names
        - Control flow flattening
        - String encryption patterns
        - Anti-debugging techniques
        - Proxy method calls

        Args:
            instance_name: Optional instance name.

        Returns:
            Obfuscation score (0-100), confidence, detected obfuscators, and detailed indicators.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        return await make_request(instance, "GET", "/analysis/obfuscation")
