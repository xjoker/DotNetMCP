"""
Transfer Tools - MCP tools for large file upload/download via temporary tokens

Tools for creating transfer tokens, uploading large files, and downloading large content
bypassing MCP message size limits.
"""

from fastmcp import FastMCP

from ..instance_registry import InstanceRegistry
from ..config import make_request


def register_tools(mcp: FastMCP):
    """Register transfer management tools with the MCP server."""

    @mcp.tool("create_transfer_token")
    async def create_transfer_token(
        operation: str,
        resource_type: str,
        timeout_seconds: int = 120,
        instance_name: str = None
    ) -> dict:
        """
        Create a temporary transfer token for large file upload/download.
        
        Args:
            operation: Operation type - "upload" or "download"
            resource_type: Resource type - "resource", "code", or "assembly"
            timeout_seconds: Token expiration timeout in seconds (default: 120)
            instance_name: Optional instance name. Uses default if not specified.
        
        Returns:
            token: Transfer token string (32 chars)
            expires_at: Expiration time in ISO format
            expires_in: Expiration timeout in seconds
            transfer_url: Base URL for transfer endpoints
        
        Usage:
            # Create upload token
            result = await create_transfer_token("upload", "resource", 180)
            
            # Use token with HTTP client
            import httpx
            async with httpx.AsyncClient() as client:
                with open("large_file.dll", "rb") as f:
                    await client.post(
                        f"{result['transfer_url']}/upload",
                        headers={"X-Transfer-Token": result["token"]},
                        files={"file": f},
                        data={"name": "resource.dll"}
                    )
            
            # Revoke token after use
            await revoke_transfer_token(result["token"])
        """
        instance = InstanceRegistry.get_instance(instance_name)
        
        # Get MVID for the request
        mvid = None
        if instance_name:
            # Try to get MVID from instance if available
            # For now, let backend handle default
            pass
        
        return await make_request(
            instance,
            "POST",
            "/transfer/token/create",
            json={
                "operation": operation,
                "resourceType": resource_type,
                "timeoutSeconds": timeout_seconds,
                "mvid": mvid
            }
        )

    @mcp.tool("revoke_transfer_token")
    async def revoke_transfer_token(token: str) -> dict:
        """
        Revoke a transfer token immediately.
        
        Should be called after completing upload/download operation
        to free up resources.
        
        Args:
            token: Transfer token to revoke
        
        Returns:
            success: Whether the token was successfully revoked
            message: Result message
        """
        instance = InstanceRegistry.get_instance()
        return await make_request(
            instance,
            "POST",
            "/transfer/token/revoke",
            json={"token": token}
        )

    @mcp.tool("get_transfer_token_status")
    async def get_transfer_token_status(token: str) -> dict:
        """
        Get the status of a transfer token.
        
        Args:
            token: Transfer token to check
        
        Returns:
            exists: Whether the token exists
            used: Whether the token has been used
            expires_in: Remaining seconds until expiration
        """
        instance = InstanceRegistry.get_instance()
        return await make_request(
            instance,
            "GET",
            f"/transfer/token/status?token={token}"
        )
