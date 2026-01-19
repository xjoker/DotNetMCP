"""
Instance Management Tools - MCP tools for managing backend instances

Tools for listing, adding, removing instances and checking status.
"""

from fastmcp import FastMCP

from ..instance_registry import InstanceRegistry
from ..config import Config


def register_tools(mcp: FastMCP):
    """Register instance management tools with the MCP server."""

    @mcp.tool("list_instances")
    async def list_instances() -> dict:
        """
        List all available backend instances.
        
        Returns:
            List of instances with name, status, and loaded assembly info.
        """
        instances = InstanceRegistry.list_instances()
        return {
            "instances": [inst.to_dict() for inst in instances],
            "default": InstanceRegistry.get_default_name()
        }

    @mcp.tool("add_instance")
    async def add_instance(
        host: str,
        port: int,
        name: str = None,
        token: str = None
    ) -> dict:
        """
        Add a new backend instance connection.
        
        Requires allow_dynamic_instances=true in config.
        
        Args:
            host: Backend service host
            port: Backend service port
            name: Optional custom name (auto-generated if not provided)
            token: Optional authentication token
        
        Returns:
            Instance info if successful.
        """
        if not Config.allow_dynamic_instances:
            return {
                "success": False,
                "message": "Dynamic instance addition is disabled. Set allow_dynamic_instances=true in config."
            }
        
        return await InstanceRegistry.add_instance(
            host=host,
            port=port,
            name=name,
            token=token,
            is_dynamic=True
        )

    @mcp.tool("remove_instance")
    async def remove_instance(name: str) -> dict:
        """
        Remove a backend instance.
        
        Only dynamic instances can be removed.
        
        Args:
            name: Instance name to remove
        
        Returns:
            Success status.
        """
        return await InstanceRegistry.remove_instance(name)

    @mcp.tool("set_default_instance")
    async def set_default_instance(name: str) -> dict:
        """
        Set the default instance for operations.
        
        Args:
            name: Instance name to set as default
        
        Returns:
            Success status.
        """
        return InstanceRegistry.set_default(name)

    @mcp.tool("get_analysis_status")
    async def get_analysis_status(instance_name: str = None) -> dict:
        """
        Get detailed analysis status and metrics.
        
        Use this before resource-intensive operations to check system status.
        
        Args:
            instance_name: Optional instance name
        
        Returns:
            Status including index percentage, memory usage, active sessions, recommendations.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        from ..config import make_request
        return await make_request(instance, "GET", "/status")

    @mcp.tool("clear_cache")
    async def clear_cache(instance_name: str = None) -> dict:
        """
        Clear analysis cache for an instance.
        
        Args:
            instance_name: Optional instance name
        
        Returns:
            Cache clear result.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        from ..config import make_request
        return await make_request(instance, "POST", "/cache/clear")

    @mcp.tool("health_check_instances")
    async def health_check_instances() -> dict:
        """
        Perform health check on all instances.
        
        Returns:
            Health status for each instance.
        """
        return await InstanceRegistry.health_check_all()
