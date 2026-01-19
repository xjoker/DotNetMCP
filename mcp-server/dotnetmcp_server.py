"""
DotNet MCP Server - Main Entry Point

MCP Server for .NET Assembly Reverse Engineering.
Connects AI (Claude/Cursor) with C# backend service for assembly analysis and modification.
"""

import asyncio
import sys
from pathlib import Path

# Add src to path
sys.path.insert(0, str(Path(__file__).parent / "src"))

from fastmcp import FastMCP
from src.server.config_loader import load_config
from src.server.instance_registry import InstanceRegistry
from src.server.tools import analysis, modification, instance, batch
from src.server.prompts import register_prompts
from src.server.resources import register_resources


def create_server() -> FastMCP:
    """Create and configure the MCP server."""
    mcp = FastMCP(
        name="DotNet MCP Server",
        version="0.1.0",
        description="AI-powered .NET Assembly Reverse Engineering via MCP Protocol",
    )
    
    # Load configuration
    config = load_config()
    
    # Initialize instance registry
    InstanceRegistry.initialize(config)
    
    # Register tools
    analysis.register_tools(mcp)
    modification.register_tools(mcp)
    instance.register_tools(mcp)
    batch.register_tools(mcp)
    
    # Register prompts
    register_prompts(mcp)
    
    # Register resources
    register_resources(mcp)
    
    return mcp


def main():
    """Main entry point."""
    mcp = create_server()
    mcp.run()


if __name__ == "__main__":
    main()
