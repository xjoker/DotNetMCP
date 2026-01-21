"""
Search Tool - Unified search across types, strings, literals, tokens

Tool: search
"""

from fastmcp import FastMCP

from ..instance_registry import InstanceRegistry
from ..config import make_request


def register_tools(mcp: FastMCP):
    """Register search tool with the MCP server."""

    @mcp.tool("search")
    async def search(
        query: str,
        mode: str = "all",
        namespace_filter: str = None,
        limit: int = 50,
        instance_name: str = None
    ) -> dict:
        """
        统一搜索工具 - 支持多种搜索模式和高级语法。
        
        ## 搜索模式 (mode)
        - all: 搜索类型和成员
        - type: 仅搜索类型
        - member: 仅搜索成员（方法、字段、属性）
        - method/field/property/event: 特定成员类型
        - literal: 搜索字符串/数字字面量
        - token: 搜索元数据Token (0x...)
        
        ## 高级语法
        - `+term`: 必须包含
        - `-term`: 排除
        - `=exact`: 精确匹配
        - `~fuzzy`: 模糊匹配
        - `/regex/`: 正则表达式
        - `"string"`: 字符串字面量
        - `0x12345678`: Token搜索
        
        Args:
            query: 搜索词，支持高级语法
            mode: 搜索模式 (all/type/member/method/field/property/event/literal/token)
            namespace_filter: 命名空间过滤 (如 "MyApp.Services")
            limit: 最大结果数 (默认50，最大500)
            instance_name: 可选的实例名称
        
        Returns:
            搜索结果列表，包含匹配项、位置和相关性评分
        
        Examples:
            # 搜索包含 "User" 的类型
            search("User", mode="type")
            
            # 精确匹配方法名
            search("=OnClick", mode="method")
            
            # 搜索字符串字面量
            search('"Hello World"', mode="literal")
            
            # 正则搜索
            search("/^Get.*Async$/", mode="method")
            
            # 组合搜索
            search("+Button -Test", mode="type", namespace_filter="MyApp.UI")
        """
        instance = InstanceRegistry.get_instance(instance_name)
        
        # Route to appropriate endpoint based on mode
        if mode == "literal":
            # String literal search
            params = {"query": query, "mode": "contains", "limit": limit}
            return await make_request(instance, "GET", "/analysis/search/strings", params=params)
        
        if mode == "token" or query.startswith("0x"):
            # Token search - use enhanced search endpoint
            body = {
                "query": query,
                "mode": "token",
                "limit": limit
            }
            if namespace_filter:
                body["namespaceFilter"] = namespace_filter
            return await make_request(instance, "POST", "/analysis/enhanced-search", json=body)
        
        # Check if using enhanced syntax
        if any(c in query for c in ['+', '-', '=', '~', '/', '"']):
            # Use enhanced search endpoint
            body = {
                "query": query,
                "mode": mode,
                "limit": limit
            }
            if namespace_filter:
                body["namespaceFilter"] = namespace_filter
            return await make_request(instance, "POST", "/analysis/enhanced-search", json=body)
        
        # Standard type search
        params = {"keyword": query, "limit": limit}
        if namespace_filter:
            params["namespace"] = namespace_filter
        return await make_request(instance, "GET", "/analysis/search/types", params=params)
