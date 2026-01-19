"""
Instance Management Tools - MCP tools for managing backend instances

Tools for listing, adding, removing instances and checking status.
Updated to match the C# REST API endpoints.
"""

from fastmcp import FastMCP

from ..instance_registry import InstanceRegistry
from ..config import Config, make_request


def register_tools(mcp: FastMCP):
    """Register instance management tools with the MCP server."""

    @mcp.tool("list_instances")
    async def list_instances() -> dict:
        """
        List all loaded assembly instances.
        
        Returns:
            List of instances with name, status, and loaded assembly info.
        """
        # Try remote backend first
        try:
            default_instance = InstanceRegistry.get_instance()
            if default_instance:
                return await make_request(default_instance, "GET", "/instance/list")
        except Exception:
            pass
        
        # Fallback to local registry
        instances = InstanceRegistry.list_instances()
        return {
            "success": True,
            "data": {
                "instances": [inst.to_dict() for inst in instances],
                "count": len(instances),
                "default_instance": InstanceRegistry.get_default_name()
            }
        }

    @mcp.tool("get_instance_info")
    async def get_instance_info(mvid: str) -> dict:
        """
        Get detailed information about a specific instance.
        
        Args:
            mvid: The MVID of the assembly instance
        
        Returns:
            Instance details including types count, memory usage.
        """
        instance = InstanceRegistry.get_instance()
        return await make_request(instance, "GET", f"/instance/{mvid}")

    @mcp.tool("set_default_instance")
    async def set_default_instance(mvid: str) -> dict:
        """
        Set the default instance for operations.
        
        Args:
            mvid: Instance MVID to set as default
        
        Returns:
            Success status.
        """
        instance = InstanceRegistry.get_instance()
        return await make_request(instance, "PUT", f"/instance/{mvid}/default")

    @mcp.tool("remove_instance")
    async def remove_instance(mvid: str) -> dict:
        """
        Remove/unload an assembly instance.
        
        Args:
            mvid: Instance MVID to remove
        
        Returns:
            Success status.
        """
        instance = InstanceRegistry.get_instance()
        return await make_request(instance, "DELETE", f"/instance/{mvid}")

    @mcp.tool("get_analysis_status")
    async def get_analysis_status(mvid: str = None) -> dict:
        """
        Get detailed analysis status and metrics.
        
        Use this before resource-intensive operations to check system status.
        
        Args:
            mvid: Optional instance MVID
        
        Returns:
            Status including loaded count, memory usage, instance state.
        """
        instance = InstanceRegistry.get_instance()
        params = {}
        if mvid:
            params["mvid"] = mvid
        return await make_request(instance, "GET", "/instance/status", params=params)

    @mcp.tool("clear_cache")
    async def clear_cache(mvid: str = None) -> dict:
        """
        Clear analysis cache for an instance.
        
        Args:
            mvid: Optional instance MVID
        
        Returns:
            Cache clear result with memory info.
        """
        instance = InstanceRegistry.get_instance()
        params = {}
        if mvid:
            params["mvid"] = mvid
        return await make_request(instance, "POST", "/instance/cache/clear", params=params)

    @mcp.tool("health_check_instances")
    async def health_check_instances() -> dict:
        """
        Perform health check on all instances.
        
        Returns:
            Health status for each instance.
        """
        instance = InstanceRegistry.get_instance()
        return await make_request(instance, "GET", "/instance/health")
