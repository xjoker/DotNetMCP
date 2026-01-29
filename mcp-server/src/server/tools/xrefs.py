"""
Cross-Reference Tool - Unified xrefs for types, methods, fields

Tool: get_xrefs
"""

from urllib.parse import quote
from fastmcp import FastMCP

from ..instance_registry import InstanceRegistry
from ..config import make_request


def register_tools(mcp: FastMCP):
    """Register cross-reference tool with the MCP server."""

    @mcp.tool("get_xrefs")
    async def get_xrefs(
        target: str | list[str],
        target_type: str = "type",
        method_name: str = None,
        limit: int = 50,
        instance_name: str = None
    ) -> dict:
        """
        获取交叉引用 - 查找类型、方法、字段的所有引用位置。
        
        ## 单个查询
        ```python
        # 类型引用
        get_xrefs("MyApp.Models.User", target_type="type")
        
        # 方法调用
        get_xrefs("MyApp.Services.UserService", target_type="method", method_name="GetUser")
        
        # 字段引用
        get_xrefs("MyApp.Models.User", target_type="field", method_name="Id")
        ```
        
        ## 批量查询 (仅类型)
        ```python
        get_xrefs(["Type1", "Type2", "Type3"], target_type="type")
        ```
        
        Args:
            target: 类型全名(单个)或类型列表(批量，最多10个)
            target_type: 目标类型 "type" | "method" | "field"
            method_name: 方法或字段名(target_type为method/field时必填)
            limit: 每个目标的最大结果数 (默认50)
            instance_name: 可选的实例名称
        
        Returns:
            引用列表，包含 source_type, source_method, line, kind
        """
        instance = InstanceRegistry.get_instance(instance_name)
        
        # Handle batch (list of type names)
        if isinstance(target, list):
            if len(target) > 10:
                return {"success": False, "message": "Maximum 10 types per batch xref request"}
            body = {"typeNames": target, "limit": limit}
            return await make_request(instance, "POST", "/analysis/batch/xrefs", json=body)
        
        # Single target
        encoded_target = quote(target, safe='')
        params = {"limit": limit}
        
        if target_type == "type":
            return await make_request(
                instance, "GET", 
                f"/analysis/xrefs/type/{encoded_target}", 
                params=params
            )
        
        if target_type == "method":
            if not method_name:
                return {"success": False, "message": "method_name required for method xrefs"}
            encoded_method = quote(method_name, safe='')
            return await make_request(
                instance, "GET",
                f"/analysis/xrefs/method/{encoded_target}/{encoded_method}",
                params=params
            )
        
        if target_type == "field":
            if not method_name:
                return {"success": False, "message": "method_name (field name) required for field xrefs"}
            # Use same endpoint pattern as method
            encoded_field = quote(method_name, safe='')
            return await make_request(
                instance, "GET",
                f"/analysis/xrefs/field/{encoded_target}/{encoded_field}",
                params=params
            )
        
        return {"success": False, "message": f"Unknown target_type: {target_type}"}
