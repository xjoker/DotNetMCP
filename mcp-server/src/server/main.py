#!/usr/bin/env python3
"""
DotNet MCP Server - Main Entry Point

This module provides the main entry point for the DotNet MCP Server.
"""

import os
import sys
import logging
from pathlib import Path

from fastmcp import FastMCP

from .config_loader import load_config
from .config import Config
from .instance_registry import InstanceRegistry
from .tools import analysis, modification, instance, batch
from .resources import register_resources
from .prompts import register_prompts

# 配置日志输出到 stdout
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    stream=sys.stdout
)
logger = logging.getLogger(__name__)


def create_app() -> FastMCP:
    """创建并配置 MCP 应用"""
    
    # 加载配置
    config_path = os.getenv("DOTNETMCP_CONFIG", None)
    if config_path:
        load_config(config_path)
    
    # 从环境变量覆盖配置
    backend_host = os.getenv("BACKEND_HOST", Config.backend_host)
    backend_port = int(os.getenv("BACKEND_PORT", str(Config.backend_port)))
    
    # 更新配置
    Config.backend_host = backend_host
    Config.backend_port = backend_port
    
    # 初始化实例注册表
    InstanceRegistry.initialize(Config)
    
    # 创建 MCP 应用
    mcp = FastMCP(
        name="DotNet MCP Server",
        description="MCP server for .NET assembly analysis and modification"
    )
    
    # 注册工具
    analysis.register_tools(mcp)
    modification.register_tools(mcp)
    instance.register_tools(mcp)
    batch.register_tools(mcp)
    
    # 注册资源和提示词
    register_resources(mcp)
    register_prompts(mcp)
    
    logger.info(f"MCP Server configured with backend at {backend_host}:{backend_port}")
    
    return mcp


def main():
    """主函数"""
    mcp = create_app()
    
    # 获取服务器配置
    host = os.getenv("MCP_HOST", "0.0.0.0")
    port = int(os.getenv("MCP_PORT", "8651"))
    
    logger.info(f"Starting DotNet MCP Server on {host}:{port}")
    
    # 运行服务器
    mcp.run(
        transport="streamable-http",
        host=host,
        port=port
    )


if __name__ == "__main__":
    main()
