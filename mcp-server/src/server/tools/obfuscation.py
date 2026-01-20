"""
Obfuscation Detection Tools - MCP tools for detecting code obfuscation

Tools for identifying obfuscated code, including identifier obfuscation,
control flow flattening, string encryption, and junk code patterns.
"""

from fastmcp import FastMCP

from ..instance_registry import InstanceRegistry
from ..config import make_request


def register_tools(mcp: FastMCP):
    """Register obfuscation detection tools with the MCP server."""

    @mcp.tool("detect_obfuscation")
    async def detect_obfuscation(
        mvid: str = None,
        instance_name: str = None
    ) -> dict:
        """
        Detect code obfuscation in the assembly.
        
        Detects the following obfuscation techniques:
        - **Identifier Obfuscation**: Suspicious type/method/field names
        - **Control Flow Flattening**: Switch-based state machines
        - **String Encryption**: Encrypted strings with decryption calls
        - **Junk Code**: Meaningless instructions (NOP, useless branches)
        
        Args:
            mvid: Optional assembly MVID
            instance_name: Optional instance name.
        
        Returns:
            Obfuscation analysis with score (0.0-1.0) and detected patterns.
            
        Obfuscation Score:
            - 0.7-1.0: Heavily obfuscated
            - 0.4-0.7: Moderately obfuscated
            - 0.2-0.4: Lightly obfuscated
            - <0.2: Minimal or no obfuscation
            
        Severity Levels:
            - High: Requires immediate attention
            - Medium: Notable obfuscation
            - Low: Minor obfuscation
            
        Example:
            # Detect all obfuscation
            result = detect_obfuscation()
            
            # Check if heavily obfuscated
            if result['data']['obfuscation_score'] > 0.7:
                print("Warning: Heavily obfuscated assembly!")
            
            # List control flow obfuscations
            cf_obf = result['data']['control_flow_obfuscations']
            for item in cf_obf:
                print(f"Method {item['method']}: {item['evidence']}")
        """
        instance = InstanceRegistry.get_instance(instance_name)
        params = {}
        if mvid:
            params["mvid"] = mvid
        return await make_request(instance, "GET", "/analysis/obfuscation", params=params)
