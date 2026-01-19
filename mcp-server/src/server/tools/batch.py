"""
Batch Tools - MCP tools for batch operations

Tools for performing multiple operations in a single call to reduce overhead.
"""

from fastmcp import FastMCP

from ..instance_registry import InstanceRegistry
from ..config import make_request


def register_tools(mcp: FastMCP):
    """Register batch tools with the MCP server."""

    @mcp.tool("batch_get_type_source")
    async def batch_get_type_source(
        type_names: list,
        language: str = "csharp",
        instance_name: str = None
    ) -> dict:
        """
        Get source code for multiple types in one call.
        
        Args:
            type_names: List of fully qualified type names (max 20)
            language: Output language: "csharp" | "il"
            instance_name: Optional instance name.
        
        Returns:
            Dict mapping type names to their source code.
        """
        if len(type_names) > 20:
            return {
                "success": False,
                "message": "Maximum 20 types per batch request"
            }
        
        instance = InstanceRegistry.get_instance(instance_name)
        body = {
            "type_names": type_names,
            "language": language
        }
        return await make_request(instance, "POST", "/analysis/batch/sources", json=body)

    @mcp.tool("batch_get_method_by_name")
    async def batch_get_method_by_name(
        methods: list,
        language: str = "csharp",
        instance_name: str = None
    ) -> dict:
        """
        Get source code for multiple methods in one call.
        
        Args:
            methods: List of {type_name, method_name} dicts (max 20)
            language: Output language: "csharp" | "il"
            instance_name: Optional instance name.
        
        Returns:
            Dict mapping method identifiers to their source code.
        """
        if len(methods) > 20:
            return {
                "success": False,
                "message": "Maximum 20 methods per batch request"
            }
        
        instance = InstanceRegistry.get_instance(instance_name)
        body = {
            "methods": methods,
            "language": language
        }
        return await make_request(instance, "POST", "/analysis/batch/methods", json=body)

    @mcp.tool("batch_get_xrefs")
    async def batch_get_xrefs(
        member_ids: list,
        limit_per_member: int = 20,
        instance_name: str = None
    ) -> dict:
        """
        Get cross-references for multiple members in one call.
        
        Args:
            member_ids: List of MemberIds (max 10)
            limit_per_member: Max xrefs per member (default 20)
            instance_name: Optional instance name.
        
        Returns:
            Dict mapping MemberIds to their xrefs.
        """
        if len(member_ids) > 10:
            return {
                "success": False,
                "message": "Maximum 10 members per batch xref request"
            }
        
        instance = InstanceRegistry.get_instance(instance_name)
        body = {
            "member_ids": member_ids,
            "limit": limit_per_member
        }
        return await make_request(instance, "POST", "/analysis/batch/xrefs", json=body)
