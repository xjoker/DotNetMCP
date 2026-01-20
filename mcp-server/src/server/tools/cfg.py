"""
Control Flow Graph Tools - MCP tools for method CFG analysis

Tools for building and visualizing control flow graphs (basic blocks, branches).
"""

from fastmcp import FastMCP

from ..instance_registry import InstanceRegistry
from ..config import make_request


def register_tools(mcp: FastMCP):
    """Register control flow graph tools with the MCP server."""

    @mcp.tool("build_control_flow_graph")
    async def build_control_flow_graph(
        type_name: str,
        method_name: str,
        format: str = "json",
        mvid: str = None,
        instance_name: str = None
    ) -> dict:
        """
        Build a control flow graph (CFG) for a method.
        
        A CFG shows the basic blocks (sequences of instructions without branches)
        and the control flow edges between them. Useful for understanding method
        logic, analyzing loops, and detecting unreachable code.
        
        Args:
            type_name: Fully qualified type name (e.g., "MyApp.Services.UserService")
            method_name: Method name (e.g., "ProcessOrder")
            format: Output format: "json" | "mermaid" (default: json)
            mvid: Optional assembly MVID
            instance_name: Optional instance name.
        
        Returns:
            Control flow graph with basic blocks, edges, and statistics.
            If format="mermaid", returns a Mermaid diagram for visualization.
            
        Basic Block Types:
            - FallThrough: Normal sequential execution
            - Branch: Unconditional jump (br)
            - ConditionalBranch: Conditional jump (brfalse, brtrue)
            - Switch: Multi-way branch
            - Return: Method return
            - Throw: Exception throw
            
        Example:
            # Get CFG as JSON
            build_control_flow_graph("MyApp.Calculator", "Calculate")
            
            # Get Mermaid diagram for visualization
            build_control_flow_graph("MyApp.Services.OrderService", "ProcessOrder", format="mermaid")
        """
        instance = InstanceRegistry.get_instance(instance_name)
        params = {"format": format}
        if mvid:
            params["mvid"] = mvid
        return await make_request(instance, "GET", f"/analysis/cfg/{type_name}/{method_name}", params=params)
