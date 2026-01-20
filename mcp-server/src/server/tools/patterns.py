"""
Design Pattern Detection Tools - MCP tools for automatically detecting design patterns

Tools for identifying common design patterns like Singleton, Factory, Observer, etc.
"""

from fastmcp import FastMCP

from ..instance_registry import InstanceRegistry
from ..config import make_request


def register_tools(mcp: FastMCP):
    """Register design pattern detection tools with the MCP server."""

    @mcp.tool("detect_design_patterns")
    async def detect_design_patterns(
        min_confidence: float = 0.5,
        mvid: str = None,
        instance_name: str = None
    ) -> dict:
        """
        Automatically detect common design patterns in the assembly.
        
        Detects the following patterns:
        - **Creational**: Singleton, Factory, Abstract Factory, Builder
        - **Structural**: Adapter, Proxy, Decorator, Facade
        - **Behavioral**: Observer, Strategy, Command, State
        
        Args:
            min_confidence: Minimum confidence threshold (0.0-1.0, default: 0.5)
            mvid: Optional assembly MVID
            instance_name: Optional instance name.
        
        Returns:
            Detected patterns with confidence scores and evidence.
            
        Confidence Levels:
            - 0.8-1.0: High confidence (very likely)
            - 0.6-0.8: Medium confidence (likely)
            - 0.4-0.6: Low confidence (possible)
            - <0.4: Very low (unlikely, filtered out by default)
            
        Example:
            # Detect all patterns with default confidence
            detect_design_patterns()
            
            # Only high-confidence detections
            detect_design_patterns(min_confidence=0.8)
            
            # Find all singletons and factories
            result = detect_design_patterns(min_confidence=0.6)
            singletons = [p for p in result['data']['patterns'] 
                         if p['pattern_type'] == 'Singleton']
        """
        instance = InstanceRegistry.get_instance(instance_name)
        params = {"minConfidence": min_confidence}
        if mvid:
            params["mvid"] = mvid
        return await make_request(instance, "GET", "/analysis/patterns", params=params)
