"""
Export Tool - Unified batch export for source code and analysis reports

Tool: export
"""

from fastmcp import FastMCP

from ..instance_registry import InstanceRegistry
from ..config import make_request


def register_tools(mcp: FastMCP):
    """Register export tool with the MCP server."""

    @mcp.tool("export")
    async def export(
        scope: str,
        targets: list[str] = None,
        namespace: str = None,
        type_name: str = None,
        language: str = "csharp",
        include_dependencies: bool = True,
        include_patterns: bool = True,
        include_obfuscation: bool = True,
        mvid: str = None,
        instance_name: str = None
    ) -> dict:
        """
        统一导出工具 - 导出源码或完整分析报告到 ZIP 文件。
        
        ## 导出作用域 (scope)
        - types: 导出多个类型的源码
        - namespace: 导出整个命名空间的源码
        - report: 导出单个类型的完整分析报告
        
        Args:
            scope: 导出作用域 "types" | "namespace" | "report"
            targets: 类型列表 (scope=types 时使用，1-100个)
            namespace: 命名空间前缀 (scope=namespace 时使用)
            type_name: 类型全名 (scope=report 时使用)
            language: 源码语言 "csharp" | "il" | "vb"
            include_dependencies: 包含依赖图 (scope=report)
            include_patterns: 包含设计模式检测 (scope=report)
            include_obfuscation: 包含混淆分析 (scope=report)
            mvid: 可选的程序集 MVID
            instance_name: 可选的实例名称
        
        Returns:
            ZIP 文件内容（二进制/base64）
        
        Examples:
            # 导出多个类型
            export(scope="types", targets=[
                "MyApp.Services.UserService",
                "MyApp.Models.User"
            ])
            
            # 导出整个命名空间
            export(scope="namespace", namespace="MyApp.Services")
            
            # 导出完整分析报告
            export(scope="report", type_name="MyApp.Services.PaymentService")
            
            # 仅导出源码和依赖
            export(
                scope="report",
                type_name="MyApp.Models.Order",
                include_patterns=False,
                include_obfuscation=False
            )
        """
        instance = InstanceRegistry.get_instance(instance_name)
        
        if scope == "types":
            if not targets:
                return {"success": False, "message": "targets required for scope=types"}
            if len(targets) > 100:
                return {"success": False, "message": "Maximum 100 types per export"}
            
            payload = {"type_names": targets, "language": language}
            if mvid:
                payload["mvid"] = mvid
            return await make_request(instance, "POST", "/analysis/export/types", json=payload)
        
        if scope == "namespace":
            if not namespace:
                return {"success": False, "message": "namespace required for scope=namespace"}
            
            payload = {"namespace_prefix": namespace, "language": language}
            if mvid:
                payload["mvid"] = mvid
            return await make_request(instance, "POST", "/analysis/export/namespace", json=payload)
        
        if scope == "report":
            if not type_name:
                return {"success": False, "message": "type_name required for scope=report"}
            
            payload = {
                "type_name": type_name,
                "include_dependencies": include_dependencies,
                "include_patterns": include_patterns,
                "include_obfuscation": include_obfuscation
            }
            if mvid:
                payload["mvid"] = mvid
            return await make_request(instance, "POST", "/analysis/export/analysis-report", json=payload)
        
        return {"success": False, "message": f"Unknown scope: {scope}"}
