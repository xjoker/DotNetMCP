"""
Modification Tools - IL code modification and assembly saving

Tools: inject_code, replace_body, save_assembly
"""

from fastmcp import FastMCP

from ..instance_registry import InstanceRegistry
from ..config import make_request


def register_tools(mcp: FastMCP):
    """Register modification tools with the MCP server."""

    @mcp.tool("inject_code")
    async def inject_code(
        method_full_name: str,
        instructions: list,
        position: str = "entry",
        mvid: str = None,
        instance_name: str = None
    ) -> dict:
        """
        在方法中注入 IL 指令。
        
        Args:
            method_full_name: 完整方法名 (如 "Namespace.Type::MethodName")
            instructions: IL 指令列表，每个指令包含:
                - opCode: 操作码 (如 "ldstr", "call", "nop")
                - intValue: 整数值 (可选)
                - stringValue: 字符串值 (可选)
            position: 注入位置 "entry" (方法入口，默认)
            mvid: 可选的程序集 MVID
            instance_name: 可选的实例名称
        
        Returns:
            成功状态和注入信息
        
        Example:
            inject_code(
                "MyApp.Program::Main",
                [
                    {"opCode": "ldstr", "stringValue": "Hello"},
                    {"opCode": "call", "stringValue": "System.Console::WriteLine"},
                    {"opCode": "nop"}
                ]
            )
        """
        instance = InstanceRegistry.get_instance(instance_name)
        body = {
            "methodFullName": method_full_name,
            "instructions": instructions
        }
        if mvid:
            body["mvid"] = mvid
        return await make_request(instance, "POST", "/modification/inject/entry", json=body)

    @mcp.tool("replace_body")
    async def replace_body(
        method_full_name: str,
        instructions: list,
        mvid: str = None,
        instance_name: str = None
    ) -> dict:
        """
        替换方法的整个方法体。
        
        Args:
            method_full_name: 完整方法名
            instructions: 新的 IL 指令列表
            mvid: 可选的程序集 MVID
            instance_name: 可选的实例名称
        
        Returns:
            成功状态和方法信息
        
        Example:
            replace_body(
                "MyApp.Calculator::Add",
                [
                    {"opCode": "ldc.i4", "intValue": 42},
                    {"opCode": "ret"}
                ]
            )
        """
        instance = InstanceRegistry.get_instance(instance_name)
        body = {
            "methodFullName": method_full_name,
            "instructions": instructions
        }
        if mvid:
            body["mvid"] = mvid
        return await make_request(instance, "POST", "/modification/replace/body", json=body)

    @mcp.tool("save_assembly")
    async def save_assembly(
        output_path: str,
        mvid: str = None,
        instance_name: str = None
    ) -> dict:
        """
        保存修改后的程序集到文件。
        
        Args:
            output_path: 输出文件路径
            mvid: 可选的程序集 MVID
            instance_name: 可选的实例名称
        
        Returns:
            成功状态和保存信息
        
        Example:
            save_assembly("/tmp/modified_assembly.dll")
        """
        instance = InstanceRegistry.get_instance(instance_name)
        body = {"outputPath": output_path}
        if mvid:
            body["mvid"] = mvid
        return await make_request(instance, "POST", "/modification/save", json=body)
