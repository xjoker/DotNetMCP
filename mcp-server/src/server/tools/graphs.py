"""
Graph Analysis Tools - Call graphs and Control Flow Graphs

Tools: build_call_graph, build_cfg
"""

from urllib.parse import quote
from fastmcp import FastMCP

from ..instance_registry import InstanceRegistry
from ..config import make_request


def register_tools(mcp: FastMCP):
    """Register graph analysis tools with the MCP server."""

    @mcp.tool("build_call_graph")
    async def build_call_graph(
        type_name: str,
        method_name: str,
        direction: str = "callees",
        max_depth: int = 3,
        max_nodes: int = 100,
        enhanced: bool = False,
        detect_recursion: bool = False,
        instance_name: str = None
    ) -> dict:
        """
        构建方法调用图。
        
        ## 基础模式
        展示方法之间的直接调用关系。
        
        ## 增强模式 (enhanced=True)
        额外分析:
        - 委托调用 (ldftn/ldvirtftn)
        - 反射调用 (GetMethod/Invoke)
        - Lambda 表达式
        - 虚方法多态解析
        
        ## 递归检测 (detect_recursion=True)
        检测直接和间接递归调用。
        
        Args:
            type_name: 类型全名
            method_name: 起始方法名
            direction: 分析方向 "callees" | "callers" | "both"
            max_depth: 最大深度 (默认3)
            max_nodes: 最大节点数 (默认100)
            enhanced: 启用增强分析（委托/反射/Lambda）
            detect_recursion: 检测递归调用
            instance_name: 可选的实例名称
        
        Returns:
            调用图，包含节点、边和统计信息
            
        Examples:
            # 基础调用图
            build_call_graph("MyApp.Program", "Main")
            
            # 增强分析
            build_call_graph("MyApp.Services.EventBus", "Publish", enhanced=True)
            
            # 递归检测
            build_call_graph("MyApp.Algorithms.Sort", "QuickSort", detect_recursion=True)
        """
        instance = InstanceRegistry.get_instance(instance_name)
        encoded_type = quote(type_name, safe='')
        encoded_method = quote(method_name, safe='')
        
        # Use enhanced endpoint if enhanced features requested
        if enhanced or detect_recursion:
            body = {
                "instanceId": None  # Uses default
            }
            result = await make_request(instance, "POST", "/analysis/enhanced-callgraph", json=body)
            
            if detect_recursion:
                recursion_result = await make_request(instance, "POST", "/analysis/detect-recursion", json=body)
                if result.get("success"):
                    result["recursions"] = recursion_result.get("recursions", [])
            
            return result
        
        # Standard call graph
        params = {
            "direction": direction,
            "max_depth": max_depth,
            "max_nodes": max_nodes
        }
        return await make_request(
            instance, "GET", 
            f"/analysis/callgraph/{encoded_type}/{encoded_method}", 
            params=params
        )

    @mcp.tool("build_cfg")
    async def build_cfg(
        type_name: str,
        method_name: str,
        format: str = "json",
        include_dominators: bool = False,
        include_dataflow: bool = False,
        instance_name: str = None
    ) -> dict:
        """
        构建方法的控制流图 (CFG)。
        
        CFG 展示方法内的基本块和控制流边，用于理解程序逻辑、分析循环、检测不可达代码。
        
        ## 基本块类型
        - FallThrough: 顺序执行
        - Branch: 无条件跳转 (br)
        - ConditionalBranch: 条件跳转 (brfalse, brtrue)
        - Switch: 多路分支
        - Return: 方法返回
        - Throw: 异常抛出
        
        ## 支配树分析 (include_dominators=True)
        计算:
        - 支配树 (Dominator Tree)
        - 后支配树 (Post-Dominator Tree)
        - 支配边界 (Dominance Frontier)
        - 控制依赖 (Control Dependence)
        
        ## 数据流分析 (include_dataflow=True)
        计算:
        - 活跃变量 (Liveness Analysis)
        - 到达定义 (Reaching Definitions)
        
        Args:
            type_name: 类型全名
            method_name: 方法名
            format: 输出格式 "json" | "mermaid"
            include_dominators: 包含支配树分析
            include_dataflow: 包含数据流分析
            instance_name: 可选的实例名称
        
        Returns:
            CFG 包含基本块、边和统计信息
            format="mermaid" 时返回 Mermaid 图表字符串
            
        Examples:
            # 基础 CFG
            build_cfg("MyApp.Calculator", "Calculate")
            
            # Mermaid 可视化
            build_cfg("MyApp.Services.OrderService", "ProcessOrder", format="mermaid")
            
            # 完整分析
            build_cfg("MyApp.Compiler", "Optimize", include_dominators=True, include_dataflow=True)
        """
        instance = InstanceRegistry.get_instance(instance_name)
        
        # Basic CFG
        params = {"format": format}
        result = await make_request(
            instance, "GET", 
            f"/analysis/cfg/{type_name}/{method_name}", 
            params=params
        )
        
        # Add dominator analysis if requested
        if include_dominators and result.get("success"):
            body = {
                "typeName": type_name,
                "methodName": method_name
            }
            dom_result = await make_request(instance, "POST", "/analysis/dominators", json=body)
            if dom_result.get("success"):
                result["dominators"] = {
                    "immediateDominators": dom_result.get("immediateDominators"),
                    "dominanceFrontier": dom_result.get("dominanceFrontier"),
                    "controlDependence": dom_result.get("controlDependence")
                }
        
        # Add dataflow analysis if requested
        if include_dataflow and result.get("success"):
            body = {
                "typeName": type_name,
                "methodName": method_name
            }
            df_result = await make_request(instance, "POST", "/analysis/dataflow", json=body)
            if df_result.get("success"):
                result["dataflow"] = {
                    "liveIn": df_result.get("liveIn"),
                    "liveOut": df_result.get("liveOut"),
                    "definitionCount": df_result.get("definitionCount")
                }
        
        return result
