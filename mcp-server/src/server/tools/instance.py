"""
Instance Management Tools - Streamlined instance management

Tools: list_instances, set_default_instance, remove_instance, clear_cache
"""

from fastmcp import FastMCP

from ..instance_registry import InstanceRegistry
from ..config import Config, make_request


def register_tools(mcp: FastMCP):
    """Register instance management tools with the MCP server."""

    @mcp.tool("list_instances")
    async def list_instances(include_health: bool = True) -> dict:
        """
        列出所有已加载的程序集实例，包含详细信息和健康状态。
        
        Args:
            include_health: 是否包含健康检查 (默认True)
        
        Returns:
            实例列表，包含:
            - name: 实例名称
            - mvid: 模块版本ID
            - assembly_name: 程序集名称
            - version: 版本号
            - types_count: 类型数量
            - memory_usage: 内存使用
            - status: 状态 (connected/disconnected)
            - is_default: 是否为默认实例
        """
        # Try remote backend first
        try:
            default_instance = InstanceRegistry.get_instance()
            if default_instance:
                result = await make_request(default_instance, "GET", "/instance/list")
                
                # Add health info if requested
                if include_health and result.get("success"):
                    try:
                        health = await make_request(default_instance, "GET", "/instance/health")
                        result["health"] = health.get("data", {})
                    except Exception:
                        result["health"] = {"error": "Health check failed"}
                
                # Add status info
                try:
                    status = await make_request(default_instance, "GET", "/instance/status")
                    result["status"] = status.get("data", {})
                except Exception:
                    pass
                
                return result
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

    @mcp.tool("set_default_instance")
    async def set_default_instance(mvid: str) -> dict:
        """
        设置默认实例。
        
        后续操作会自动使用默认实例，无需每次指定 instance_name。
        
        Args:
            mvid: 实例 MVID
        
        Returns:
            成功状态
        """
        instance = InstanceRegistry.get_instance()
        return await make_request(instance, "PUT", f"/instance/{mvid}/default")

    @mcp.tool("remove_instance")
    async def remove_instance(mvid: str) -> dict:
        """
        移除/卸载程序集实例。
        
        Args:
            mvid: 实例 MVID
        
        Returns:
            成功状态
        """
        instance = InstanceRegistry.get_instance()
        return await make_request(instance, "DELETE", f"/instance/{mvid}")

    @mcp.tool("clear_cache")
    async def clear_cache(mvid: str = None) -> dict:
        """
        清除分析缓存。
        
        Args:
            mvid: 可选的实例 MVID，不指定则清除所有缓存
        
        Returns:
            缓存清除结果，包含内存信息
        """
        instance = InstanceRegistry.get_instance()
        params = {"mvid": mvid} if mvid else {}
        return await make_request(instance, "POST", "/instance/cache/clear", params=params)
