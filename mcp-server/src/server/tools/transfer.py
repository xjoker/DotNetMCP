"""
Transfer Tool - Large file upload/download via temporary tokens

Tool: create_transfer_token
"""

from fastmcp import FastMCP

from ..instance_registry import InstanceRegistry
from ..config import make_request


def register_tools(mcp: FastMCP):
    """Register transfer tool with the MCP server."""

    @mcp.tool("create_transfer_token")
    async def create_transfer_token(
        operation: str,
        resource_type: str = "resource",
        timeout_seconds: int = 120,
        instance_name: str = None
    ) -> dict:
        """
        创建临时传输令牌，用于大文件上传/下载。
        
        令牌会自动过期，无需手动撤销。
        
        Args:
            operation: 操作类型 "upload" | "download"
            resource_type: 资源类型 "resource" | "code" | "assembly"
            timeout_seconds: 令牌有效期（秒，默认120）
            instance_name: 可选的实例名称
        
        Returns:
            - token: 传输令牌 (32字符)
            - expires_at: 过期时间 (ISO格式)
            - expires_in: 有效期（秒）
            - transfer_url: 传输端点基础URL
        
        Usage:
            ```python
            # 创建上传令牌
            result = await create_transfer_token("upload", "resource", 180)
            
            # 使用 HTTP 客户端上传
            import httpx
            async with httpx.AsyncClient() as client:
                with open("large_file.dll", "rb") as f:
                    await client.post(
                        f"{result['transfer_url']}/upload",
                        headers={"X-Transfer-Token": result["token"]},
                        files={"file": f},
                        data={"name": "resource.dll"}
                    )
            
            # 令牌会自动过期，无需手动撤销
            ```
        """
        instance = InstanceRegistry.get_instance(instance_name)
        
        return await make_request(
            instance,
            "POST",
            "/transfer/token/create",
            json={
                "operation": operation,
                "resourceType": resource_type,
                "timeoutSeconds": timeout_seconds
            }
        )
