"""
Detection Tool - Unified detection for patterns and obfuscation

Tool: detect
"""

from fastmcp import FastMCP

from ..instance_registry import InstanceRegistry
from ..config import make_request


def register_tools(mcp: FastMCP):
    """Register detection tool with the MCP server."""

    @mcp.tool("detect")
    async def detect(
        type: str = "all",
        min_confidence: float = 0.5,
        mvid: str = None,
        instance_name: str = None
    ) -> dict:
        """
        统一检测工具 - 检测设计模式和代码混淆。
        
        ## 检测类型 (type)
        - all: 同时检测模式和混淆
        - patterns: 仅检测设计模式
        - obfuscation: 仅检测代码混淆
        
        ## 设计模式检测
        - 创建型: Singleton, Factory, Abstract Factory, Builder
        - 结构型: Adapter, Proxy, Decorator, Facade
        - 行为型: Observer, Strategy, Command, State
        
        ## 混淆检测
        - 标识符混淆: 非法类型/方法/字段名
        - 控制流平坦化: 基于 Switch 的状态机
        - 字符串加密: 加密字符串和解密调用
        - 垃圾代码: 无意义指令
        
        Args:
            type: 检测类型 "all" | "patterns" | "obfuscation"
            min_confidence: 最小置信度 (0.0-1.0，默认0.5)
            mvid: 可选的程序集 MVID
            instance_name: 可选的实例名称
        
        Returns:
            - patterns: 检测到的设计模式列表
            - obfuscation: 混淆评分和检测到的特征
        
        Examples:
            # 完整检测
            detect()
            
            # 仅检测设计模式（高置信度）
            detect(type="patterns", min_confidence=0.8)
            
            # 仅检测混淆
            detect(type="obfuscation")
        """
        instance = InstanceRegistry.get_instance(instance_name)
        params = {"mvid": mvid} if mvid else {}
        
        result = {"success": True, "data": {}}
        
        if type in ("all", "patterns"):
            params["minConfidence"] = min_confidence
            patterns_result = await make_request(instance, "GET", "/analysis/patterns", params=params)
            result["data"]["patterns"] = patterns_result.get("data", {})
        
        if type in ("all", "obfuscation"):
            obf_params = {"mvid": mvid} if mvid else {}
            obf_result = await make_request(instance, "GET", "/analysis/obfuscation", params=obf_params)
            result["data"]["obfuscation"] = obf_result.get("data", {})
        
        return result
