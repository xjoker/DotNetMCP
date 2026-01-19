"""
Modification Tools - MCP tools for .NET assembly modification

Tools for modifying method bodies, adding/removing members, and managing modify sessions.
"""

from fastmcp import FastMCP

from ..instance_registry import InstanceRegistry
from ..config import make_request


def register_tools(mcp: FastMCP):
    """Register modification tools with the MCP server."""

    @mcp.tool("begin_modify_session")
    async def begin_modify_session(
        assembly_path: str = None,
        instance_name: str = None
    ) -> dict:
        """
        Begin a modification session for an assembly.
        
        All modifications are staged until commit. Use rollback to discard changes.
        
        Args:
            assembly_path: Path to assembly (uses loaded assembly if not specified)
            instance_name: Optional instance name.
        
        Returns:
            Session ID and assembly info.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        body = {}
        if assembly_path:
            body["path"] = assembly_path
        return await make_request(instance, "POST", "/modify/session/begin", json=body)

    @mcp.tool("replace_method_body")
    async def replace_method_body(
        session_id: str,
        member_id: str,
        new_body: str,
        format: str = "csharp",
        instance_name: str = None
    ) -> dict:
        """
        Replace the body of a method.
        
        Args:
            session_id: Modify session ID
            member_id: Target method MemberId
            new_body: New method body content
            format: Body format: "csharp" | "il"
            instance_name: Optional instance name.
        
        Returns:
            Validation result and warnings.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        body = {
            "session_id": session_id,
            "member_id": member_id,
            "body": new_body,
            "format": format
        }
        return await make_request(instance, "POST", "/modify/method/replace", json=body)

    @mcp.tool("inject_il")
    async def inject_il(
        session_id: str,
        member_id: str,
        position: str,
        instructions: list,
        anchor_offset: str = None,
        instance_name: str = None
    ) -> dict:
        """
        Inject IL instructions into a method.
        
        Args:
            session_id: Modify session ID
            member_id: Target method MemberId
            position: "start" | "end" | "before" | "after"
            instructions: List of IL instructions
            anchor_offset: IL offset for before/after positioning
            instance_name: Optional instance name.
        
        Returns:
            New offsets and validation result.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        body = {
            "session_id": session_id,
            "member_id": member_id,
            "position": position,
            "instructions": instructions
        }
        if anchor_offset:
            body["anchor"] = anchor_offset
        return await make_request(instance, "POST", "/modify/method/inject", json=body)

    @mcp.tool("add_member")
    async def add_member(
        session_id: str,
        target_type_id: str,
        kind: str,
        name: str,
        signature: dict,
        body: str = None,
        instance_name: str = None
    ) -> dict:
        """
        Add a new member to a type.
        
        Args:
            session_id: Modify session ID
            target_type_id: Target type MemberId
            kind: "method" | "field" | "property"
            name: Member name
            signature: Signature definition
            body: Method body (for methods, optional)
            instance_name: Optional instance name.
        
        Returns:
            New member's temporary MemberId.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        payload = {
            "session_id": session_id,
            "target_type_id": target_type_id,
            "kind": kind,
            "name": name,
            "signature": signature
        }
        if body:
            payload["body"] = body
        return await make_request(instance, "POST", "/modify/member/add", json=payload)

    @mcp.tool("remove_member")
    async def remove_member(
        session_id: str,
        member_id: str,
        force: bool = False,
        instance_name: str = None
    ) -> dict:
        """
        Remove a member from its type.
        
        Args:
            session_id: Modify session ID
            member_id: Member to remove
            force: Force removal even if referenced
            instance_name: Optional instance name.
        
        Returns:
            Affected references list.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        body = {
            "session_id": session_id,
            "member_id": member_id,
            "force": force
        }
        return await make_request(instance, "POST", "/modify/member/remove", json=body)

    @mcp.tool("commit_session")
    async def commit_session(
        session_id: str,
        output_path: str,
        instance_name: str = None
    ) -> dict:
        """
        Commit all modifications and write the new assembly.
        
        Args:
            session_id: Modify session ID
            output_path: Path for the modified assembly
            instance_name: Optional instance name.
        
        Returns:
            New assembly info, ID mapping table, validation result.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        body = {
            "session_id": session_id,
            "output_path": output_path
        }
        return await make_request(instance, "POST", "/modify/session/commit", json=body)

    @mcp.tool("rollback_session")
    async def rollback_session(
        session_id: str,
        instance_name: str = None
    ) -> dict:
        """
        Discard all modifications and close the session.
        
        Args:
            session_id: Modify session ID
            instance_name: Optional instance name.
        
        Returns:
            List of discarded changes.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        body = {"session_id": session_id}
        return await make_request(instance, "POST", "/modify/session/rollback", json=body)
