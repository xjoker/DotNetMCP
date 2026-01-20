"""
Batch Export Tools - MCP tools for batch downloading source code

Tools for exporting multiple types/methods/namespaces to ZIP files.
"""

from fastmcp import FastMCP

from ..instance_registry import InstanceRegistry
from ..config import make_request


def register_tools(mcp: FastMCP):
    """Register batch export tools with the MCP server."""

    @mcp.tool("export_types_zip")
    async def export_types_zip(
        type_names: list[str],
        language: str = "csharp",
        mvid: str = None,
        instance_name: str = None
    ) -> dict:
        """
        Export multiple type source codes to a ZIP file.
        
        Args:
            type_names: List of fully qualified type names (1-100 types)
            language: Source language ("csharp" | "il" | "vb", default: csharp)
            mvid: Optional assembly MVID
            instance_name: Optional instance name.
        
        Returns:
            Binary ZIP file containing source code files.
            
        Example:
            # Export multiple types
            export_types_zip([
                "MyApp.Services.UserService",
                "MyApp.Models.User",
                "MyApp.Controllers.HomeController"
            ])
        """
        instance = InstanceRegistry.get_instance(instance_name)
        payload = {
            "type_names": type_names,
            "language": language
        }
        if mvid:
            payload["mvid"] = mvid
        return await make_request(instance, "POST", "/analysis/export/types", json=payload)

    @mcp.tool("export_namespace_zip")
    async def export_namespace_zip(
        namespace_prefix: str,
        language: str = "csharp",
        mvid: str = None,
        instance_name: str = None
    ) -> dict:
        """
        Export an entire namespace to a ZIP file.
        
        Args:
            namespace_prefix: Namespace prefix (e.g., "MyApp.Services")
            language: Source language ("csharp" | "il" | "vb", default: csharp)
            mvid: Optional assembly MVID
            instance_name: Optional instance name.
        
        Returns:
            Binary ZIP file containing all types in the namespace.
            
        Example:
            # Export entire namespace
            export_namespace_zip("MyApp.Services")
            
            # Export with IL code
            export_namespace_zip("MyApp.Core", language="il")
        """
        instance = InstanceRegistry.get_instance(instance_name)
        payload = {
            "namespace_prefix": namespace_prefix,
            "language": language
        }
        if mvid:
            payload["mvid"] = mvid
        return await make_request(instance, "POST", "/analysis/export/namespace", json=payload)

    @mcp.tool("export_analysis_report")
    async def export_analysis_report(
        type_name: str,
        include_dependencies: bool = True,
        include_patterns: bool = True,
        include_obfuscation: bool = True,
        mvid: str = None,
        instance_name: str = None
    ) -> dict:
        """
        Export complete analysis report for a type to a ZIP file.
        
        The ZIP contains:
        - Source code (.cs file)
        - Dependency graph (.mmd Mermaid file, if enabled)
        - Design patterns detected (.md file, if any patterns found)
        - Obfuscation analysis (.md file, if obfuscation detected)
        
        Args:
            type_name: Fully qualified type name
            include_dependencies: Include dependency graph (default: True)
            include_patterns: Include pattern detection (default: True)
            include_obfuscation: Include obfuscation analysis (default: True)
            mvid: Optional assembly MVID
            instance_name: Optional instance name.
        
        Returns:
            Binary ZIP file containing comprehensive analysis report.
            
        Example:
            # Full analysis report
            export_analysis_report("MyApp.Services.PaymentService")
            
            # Only source and dependencies
            export_analysis_report(
                "MyApp.Models.Order",
                include_patterns=False,
                include_obfuscation=False
            )
        """
        instance = InstanceRegistry.get_instance(instance_name)
        payload = {
            "type_name": type_name,
            "include_dependencies": include_dependencies,
            "include_patterns": include_patterns,
            "include_obfuscation": include_obfuscation
        }
        if mvid:
            payload["mvid"] = mvid
        return await make_request(instance, "POST", "/analysis/export/analysis-report", json=payload)
