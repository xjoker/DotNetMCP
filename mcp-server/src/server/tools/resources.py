"""
Resource Management Tools - MCP tools for managing embedded resources

Tools for listing, reading, adding, replacing, and removing embedded resources.
"""

from fastmcp import FastMCP
import base64

from ..instance_registry import InstanceRegistry
from ..config import make_request


def register_tools(mcp: FastMCP):
    """Register resource management tools with the MCP server."""

    @mcp.tool("list_resources")
    async def list_resources(instance_name: str = None) -> dict:
        """
        List all embedded resources in the assembly.
        
        Args:
            instance_name: Optional instance name. Uses default if not specified.
        
        Returns:
            List of resources with name, type, visibility, and size.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        return await make_request(instance, "GET", "/resources/list")

    @mcp.tool("get_resource")
    async def get_resource(
        resource_name: str,
        return_base64: bool = True,
        instance_name: str = None
    ) -> dict:
        """
        Get resource content by name.
        
        Args:
            resource_name: Name of the resource to retrieve
            return_base64: If true, return content as base64; if false, return hex
            instance_name: Optional instance name. Uses default if not specified.
        
        Returns:
            Resource data including name, size, content preview, and full content.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        return await make_request(
            instance, 
            "GET", 
            f"/resources/{resource_name}",
            params={"returnBase64": str(return_base64).lower()}
        )

    @mcp.tool("add_resource")
    async def add_resource(
        name: str,
        content_text: str = None,
        content_base64: str = None,
        is_public: bool = True,
        instance_name: str = None
    ) -> dict:
        """
        Add a new embedded resource.
        
        Args:
            name: Resource name (e.g., "config.json", "data.bin")
            content_text: Text content (for text resources)
            content_base64: Base64-encoded content (for binary resources)
            is_public: Whether the resource is public (default: true)
            instance_name: Optional instance name. Uses default if not specified.
        
        Returns:
            Success status and resource info.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        return await make_request(
            instance,
            "POST",
            "/resources/add",
            json={
                "name": name,
                "contentText": content_text,
                "contentBase64": content_base64,
                "isPublic": is_public
            }
        )

    @mcp.tool("replace_resource")
    async def replace_resource(
        name: str,
        content_text: str = None,
        content_base64: str = None,
        is_public: bool = None,
        instance_name: str = None
    ) -> dict:
        """
        Replace an existing embedded resource.
        
        Args:
            name: Resource name to replace
            content_text: New text content (for text resources)
            content_base64: New base64-encoded content (for binary resources)
            is_public: Optional new visibility (None to keep existing)
            instance_name: Optional instance name. Uses default if not specified.
        
        Returns:
            Success status and updated resource info.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        return await make_request(
            instance,
            "PUT",
            "/resources/replace",
            json={
                "name": name,
                "contentText": content_text,
                "contentBase64": content_base64,
                "isPublic": is_public
            }
        )

    @mcp.tool("remove_resource")
    async def remove_resource(
        resource_name: str,
        instance_name: str = None
    ) -> dict:
        """
        Remove a resource by name.
        
        Args:
            resource_name: Name of the resource to remove
            instance_name: Optional instance name. Uses default if not specified.
        
        Returns:
            Success status.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        return await make_request(instance, "DELETE", f"/resources/{resource_name}")

    @mcp.tool("export_all_resources")
    async def export_all_resources(instance_name: str = None) -> dict:
        """
        Export all resources to a dictionary.
        
        Args:
            instance_name: Optional instance name. Uses default if not specified.
        
        Returns:
            Dictionary mapping resource names to base64-encoded content.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        return await make_request(instance, "POST", "/resources/export")
