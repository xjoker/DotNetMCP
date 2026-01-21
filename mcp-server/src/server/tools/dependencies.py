"""
Dependency Analysis Tool - Assembly and type dependencies

Tool: get_dependencies
"""

from urllib.parse import quote
from fastmcp import FastMCP

from ..instance_registry import InstanceRegistry
from ..config import make_request


def register_tools(mcp: FastMCP):
    """Register dependency analysis tool with the MCP server."""

    @mcp.tool("get_dependencies")
    async def get_dependencies(
        scope: str = "assembly",
        type_name: str = None,
        include_system: bool = False,
        max_depth: int = 3,
        format: str = "json",
        mvid: str = None,
        instance_name: str = None
    ) -> dict:
        """
        获取依赖关系图。
        
        ## 作用域 (scope)
        - assembly: 程序集级别依赖
        - type: 类型级别依赖（需指定 type_name）
        
        Args:
            scope: 分析作用域 "assembly" | "type"
            type_name: 类型全名 (scope=type 时必填)
            include_system: 包含 System.*/Microsoft.* 程序集 (默认False)
            max_depth: 最大遍历深度 (1-5，默认3，仅 scope=type)
            format: 输出格式 "json" | "mermaid"
            mvid: 可选的程序集 MVID
            instance_name: 可选的实例名称
        
        Returns:
            依赖关系图，包含节点和边
            format="mermaid" 时返回 Mermaid 图表字符串
        
        Examples:
            # 程序集依赖
            get_dependencies()
            
            # 程序集依赖 Mermaid 图
            get_dependencies(format="mermaid", include_system=True)
            
            # 类型依赖
            get_dependencies(scope="type", type_name="MyApp.Services.UserService")
        """
        instance = InstanceRegistry.get_instance(instance_name)
        
        if scope == "assembly":
            params = {
                "includeSystem": str(include_system).lower(),
                "format": format
            }
            if mvid:
                params["mvid"] = mvid
            return await make_request(instance, "GET", "/analysis/dependencies/assembly", params=params)
        
        if scope == "type":
            if not type_name:
                return {"success": False, "message": "type_name required for scope=type"}
            
            params = {
                "maxDepth": max_depth,
                "format": format
            }
            if mvid:
                params["mvid"] = mvid
            return await make_request(
                instance, "GET", 
                f"/analysis/dependencies/type/{type_name}", 
                params=params
            )
        
        return {"success": False, "message": f"Unknown scope: {scope}"}
