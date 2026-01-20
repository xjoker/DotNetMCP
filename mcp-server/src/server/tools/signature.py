"""
Signature Management Tools - MCP tools for assembly signing

Tools for managing assembly strong-name signatures.
"""

from fastmcp import FastMCP

from ..instance_registry import InstanceRegistry
from ..config import make_request


def register_tools(mcp: FastMCP):
    """Register signature management tools with the MCP server."""

    @mcp.tool("get_signature")
    async def get_signature(
        mvid: str = None,
        instance_name: str = None
    ) -> dict:
        """
        Get assembly signature information.
        
        Args:
            mvid: Optional assembly MVID
            instance_name: Optional instance name.
        
        Returns:
            Signature info including public key token, strong name status.
            
        Example:
            # Check if assembly is signed
            result = get_signature()
            if result['data']['is_strong_named']:
                print(f"Signed with token: {result['data']['public_key_token']}")
        """
        instance = InstanceRegistry.get_instance(instance_name)
        params = {"mvid": mvid} if mvid else {}
        return await make_request(instance, "GET", "/signature", params=params)

    @mcp.tool("remove_signature")
    async def remove_signature(
        mvid: str = None,
        instance_name: str = None
    ) -> dict:
        """
        Remove the strong-name signature from an assembly.
        
        After removal, the assembly will not be strong-named.
        
        Args:
            mvid: Optional assembly MVID
            instance_name: Optional instance name.
        
        Returns:
            Success status.
            
        Note:
            This is useful when you need to modify a signed assembly.
            The assembly must be re-signed before deployment.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        payload = {"mvid": mvid} if mvid else {}
        return await make_request(instance, "POST", "/signature/remove", json=payload)
