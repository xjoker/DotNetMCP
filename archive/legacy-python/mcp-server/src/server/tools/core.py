"""
Core Analysis Tools - Fundamental tools for .NET assembly analysis

Tools: get_assembly_info, get_type_source, get_method_source, get_type_info
"""

from urllib.parse import quote
from fastmcp import FastMCP

from ..instance_registry import InstanceRegistry
from ..config import make_request


def register_tools(mcp: FastMCP):
    """Register core analysis tools with the MCP server."""

    @mcp.tool("get_assembly_info")
    async def get_assembly_info(instance_name: str = None) -> dict:
        """
        获取程序集基本信息。推荐作为首次调用。
        
        返回程序集名称、版本、目标框架、类型统计等信息。
        
        Args:
            instance_name: 可选的实例名称，不指定则使用默认实例
        
        Returns:
            程序集元数据，包括 name, version, framework, statistics
        """
        instance = InstanceRegistry.get_instance(instance_name)
        return await make_request(instance, "GET", "/assembly/info")

    @mcp.tool("get_type_source")
    async def get_type_source(
        type_names: str | list[str],
        language: str = "csharp",
        instance_name: str = None
    ) -> dict:
        """
        获取类型的反编译源码。支持单个或批量获取。
        
        Args:
            type_names: 类型全名，可以是单个字符串或列表(最多20个)
                示例: "MyNamespace.MyClass" 或 ["Type1", "Type2"]
            language: 输出语言 "csharp" | "il"
            instance_name: 可选的实例名称
        
        Returns:
            单个类型: 反编译源码和行号映射
            批量: 字典 {类型名: 源码}
        """
        instance = InstanceRegistry.get_instance(instance_name)
        
        # Handle single type
        if isinstance(type_names, str):
            encoded_type = quote(type_names, safe='')
            return await make_request(
                instance, "GET", 
                f"/analysis/type/{encoded_type}/source",
                params={"language": language}
            )
        
        # Handle batch (list)
        if len(type_names) > 20:
            return {"success": False, "message": "Maximum 20 types per batch request"}
        
        body = {"typeNames": type_names, "language": language}
        return await make_request(instance, "POST", "/analysis/batch/sources", json=body)

    @mcp.tool("get_method_source")
    async def get_method_source(
        methods: str | list[dict],
        type_name: str = None,
        language: str = "csharp",
        instance_name: str = None
    ) -> dict:
        """
        获取方法的反编译源码。支持单个或批量获取。
        
        单个方法:
            get_method_source("MethodName", type_name="Namespace.Class")
        
        批量方法:
            get_method_source([
                {"type_name": "Class1", "method_name": "Method1"},
                {"type_name": "Class2", "method_name": "Method2"}
            ])
        
        Args:
            methods: 方法名(单个)或方法列表(批量，最多20个)
            type_name: 单个方法时必填，类型全名
            language: 输出语言 "csharp" | "il"
            instance_name: 可选的实例名称
        
        Returns:
            方法源码及签名
        """
        instance = InstanceRegistry.get_instance(instance_name)
        
        # Handle single method
        if isinstance(methods, str):
            if not type_name:
                return {"success": False, "message": "type_name required for single method"}
            encoded_type = quote(type_name, safe='')
            encoded_method = quote(methods, safe='')
            return await make_request(
                instance, "GET",
                f"/analysis/type/{encoded_type}/method/{encoded_method}",
                params={"language": language}
            )
        
        # Handle batch (list of dicts)
        if len(methods) > 20:
            return {"success": False, "message": "Maximum 20 methods per batch request"}
        
        body = {
            "methods": [
                {"typeName": m.get("type_name", m.get("typeName", "")), 
                 "methodName": m.get("method_name", m.get("methodName", ""))} 
                for m in methods
            ],
            "language": language
        }
        return await make_request(instance, "POST", "/analysis/batch/methods", json=body)

    @mcp.tool("get_type_info")
    async def get_type_info(type_name: str, instance_name: str = None) -> dict:
        """
        获取类型结构信息（继承、接口、成员）。
        
        Args:
            type_name: 类型全名
            instance_name: 可选的实例名称
        
        Returns:
            类型元数据，包括 base_type, interfaces, methods, fields, properties
        """
        instance = InstanceRegistry.get_instance(instance_name)
        encoded_type = quote(type_name, safe='')
        return await make_request(instance, "GET", f"/analysis/type/{encoded_type}/info")
