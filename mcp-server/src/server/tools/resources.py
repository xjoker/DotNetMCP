"""
Resource Management Tools - Embedded resource operations

Tools: list_resources, get_resource, set_resource, remove_resource
"""

from fastmcp import FastMCP

from ..instance_registry import InstanceRegistry
from ..config import make_request


def register_tools(mcp: FastMCP):
    """Register resource management tools with the MCP server."""

    @mcp.tool("list_resources")
    async def list_resources(
        export_all: bool = False,
        instance_name: str = None
    ) -> dict:
        """
        列出程序集中的所有嵌入式资源。
        
        Args:
            export_all: 如果为 True，同时返回所有资源的 base64 内容
            instance_name: 可选的实例名称
        
        Returns:
            资源列表，包含 name, type, visibility, size
            如果 export_all=True，还包含 content (base64)
        """
        instance = InstanceRegistry.get_instance(instance_name)
        
        if export_all:
            return await make_request(instance, "POST", "/resources/export")
        
        return await make_request(instance, "GET", "/resources/list")

    @mcp.tool("get_resource")
    async def get_resource(
        resource_name: str,
        return_base64: bool = True,
        instance_name: str = None
    ) -> dict:
        """
        获取指定资源的内容。
        
        Args:
            resource_name: 资源名称
            return_base64: True 返回 base64 编码，False 返回 hex
            instance_name: 可选的实例名称
        
        Returns:
            资源数据，包含 name, size, content_preview, content
        """
        instance = InstanceRegistry.get_instance(instance_name)
        return await make_request(
            instance, 
            "GET", 
            f"/resources/{resource_name}",
            params={"returnBase64": str(return_base64).lower()}
        )

    @mcp.tool("set_resource")
    async def set_resource(
        name: str,
        content_text: str = None,
        content_base64: str = None,
        is_public: bool = True,
        upsert: bool = True,
        instance_name: str = None
    ) -> dict:
        """
        添加或替换嵌入式资源（upsert 模式）。
        
        Args:
            name: 资源名称 (如 "config.json", "data.bin")
            content_text: 文本内容 (文本资源)
            content_base64: Base64 编码内容 (二进制资源)
            is_public: 是否公开 (默认 True)
            upsert: 如果存在则替换 (默认 True)
            instance_name: 可选的实例名称
        
        Returns:
            成功状态和资源信息
        
        Example:
            # 添加文本资源
            set_resource("config.json", content_text='{"key": "value"}')
            
            # 添加二进制资源
            set_resource("image.png", content_base64="iVBORw0KGgo...")
        """
        instance = InstanceRegistry.get_instance(instance_name)
        
        body = {
            "name": name,
            "contentText": content_text,
            "contentBase64": content_base64,
            "isPublic": is_public
        }
        
        if upsert:
            # Try replace first, fallback to add if resource doesn't exist
            result = await make_request(instance, "PUT", "/resources/replace", json=body)
            if not result.get("success") and "not found" in result.get("message", "").lower():
                return await make_request(instance, "POST", "/resources/add", json=body)
            return result
        
        return await make_request(instance, "POST", "/resources/add", json=body)

    @mcp.tool("remove_resource")
    async def remove_resource(
        resource_name: str,
        instance_name: str = None
    ) -> dict:
        """
        删除指定资源。
        
        Args:
            resource_name: 资源名称
            instance_name: 可选的实例名称
        
        Returns:
            成功状态
        """
        instance = InstanceRegistry.get_instance(instance_name)
        return await make_request(instance, "DELETE", f"/resources/{resource_name}")
