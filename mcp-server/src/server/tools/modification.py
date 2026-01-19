"""
Modification Tools - MCP tools for .NET assembly modification

Tools for modifying method bodies, adding/removing members.
Updated to match the C# REST API endpoints.
"""

from fastmcp import FastMCP

from ..instance_registry import InstanceRegistry
from ..config import make_request


def register_tools(mcp: FastMCP):
    """Register modification tools with the MCP server."""

    @mcp.tool("inject_method_entry")
    async def inject_method_entry(
        method_full_name: str,
        instructions: list,
        mvid: str = None,
        instance_name: str = None
    ) -> dict:
        """
        Inject IL instructions at method entry.
        
        Args:
            method_full_name: Full method name (e.g., "Namespace.Type::MethodName")
            instructions: List of instruction objects with opcode, intValue, stringValue
            mvid: Optional assembly MVID (uses loaded assembly if not specified)
            instance_name: Optional instance name.
        
        Returns:
            Success status and injection info.
            
        Example:
            inject_method_entry(
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

    @mcp.tool("replace_method_body")
    async def replace_method_body(
        method_full_name: str,
        instructions: list,
        mvid: str = None,
        instance_name: str = None
    ) -> dict:
        """
        Replace the entire body of a method with new IL instructions.
        
        Args:
            method_full_name: Full method name
            instructions: List of instruction objects
            mvid: Optional assembly MVID
            instance_name: Optional instance name.
        
        Returns:
            Success status and method info.
            
        Example:
            replace_method_body(
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

    @mcp.tool("add_type")
    async def add_type(
        namespace: str,
        name: str,
        kind: str = "class",
        mvid: str = None,
        instance_name: str = None
    ) -> dict:
        """
        Add a new type to the assembly.
        
        Args:
            namespace: Type namespace
            name: Type name
            kind: Type kind: "class" | "interface" | "struct"
            mvid: Optional assembly MVID
            instance_name: Optional instance name.
        
        Returns:
            Success status and type full name.
            
        Example:
            add_type("MyApp.Models", "NewClass", "class")
        """
        instance = InstanceRegistry.get_instance(instance_name)
        body = {
            "namespace": namespace,
            "name": name,
            "kind": kind
        }
        if mvid:
            body["mvid"] = mvid
        return await make_request(instance, "POST", "/modification/type/add", json=body)

    @mcp.tool("add_method")
    async def add_method(
        type_full_name: str,
        name: str,
        return_type: str = "void",
        parameters: list = None,
        mvid: str = None,
        instance_name: str = None
    ) -> dict:
        """
        Add a new method to a type.
        
        Args:
            type_full_name: Full type name (e.g., "MyApp.Models.User")
            name: Method name
            return_type: Return type (default "void")
            parameters: List of parameter objects with name and type
            mvid: Optional assembly MVID
            instance_name: Optional instance name.
        
        Returns:
            Success status and method info.
            
        Example:
            add_method(
                "MyApp.Models.User",
                "GetFullName",
                "string",
                [
                    {"name": "firstName", "type": "string"},
                    {"name": "lastName", "type": "string"}
                ]
            )
        """
        instance = InstanceRegistry.get_instance(instance_name)
        body = {
            "typeFullName": type_full_name,
            "name": name,
            "returnType": return_type
        }
        if parameters:
            body["parameters"] = parameters
        if mvid:
            body["mvid"] = mvid
        return await make_request(instance, "POST", "/modification/method/add", json=body)

    @mcp.tool("save_assembly")
    async def save_assembly(
        output_path: str,
        mvid: str = None,
        instance_name: str = None
    ) -> dict:
        """
        Save the modified assembly to a file.
        
        Args:
            output_path: Path where to save the assembly
            mvid: Optional assembly MVID
            instance_name: Optional instance name.
        
        Returns:
            Success status and save info.
            
        Example:
            save_assembly("/tmp/modified_assembly.dll")
        """
        instance = InstanceRegistry.get_instance(instance_name)
        body = {
            "outputPath": output_path
        }
        if mvid:
            body["mvid"] = mvid
        return await make_request(instance, "POST", "/modification/save", json=body)
