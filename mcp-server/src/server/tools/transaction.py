"""
Transaction Management Tools - Modification session transactions

Tools: begin_transaction, commit_transaction, rollback_transaction
"""

from fastmcp import FastMCP

from ..instance_registry import InstanceRegistry
from ..config import make_request


def register_tools(mcp: FastMCP):
    """Register transaction management tools with the MCP server."""

    @mcp.tool("begin_transaction")
    async def begin_transaction(
        mvid: str = None,
        instance_name: str = None
    ) -> dict:
        """
        开始一个修改事务。
        
        创建当前程序集状态的快照，可通过 rollback_transaction 恢复。
        
        Args:
            mvid: 可选的程序集 MVID
            instance_name: 可选的实例名称
        
        Returns:
            - transaction_id: 事务ID
            - status: 状态
            - start_time: 开始时间
        
        Example:
            # 开始事务
            result = begin_transaction()
            txn_id = result['data']['transaction_id']
            
            # 进行修改...
            
            # 成功时提交
            commit_transaction(txn_id)
            
            # 失败时回滚
            rollback_transaction(txn_id)
        """
        instance = InstanceRegistry.get_instance(instance_name)
        payload = {"mvid": mvid} if mvid else {}
        return await make_request(instance, "POST", "/transaction/begin", json=payload)

    @mcp.tool("commit_transaction")
    async def commit_transaction(
        transaction_id: str,
        instance_name: str = None
    ) -> dict:
        """
        提交事务，使所有修改永久生效。
        
        Args:
            transaction_id: begin_transaction 返回的事务ID
            instance_name: 可选的实例名称
        
        Returns:
            成功状态
        """
        instance = InstanceRegistry.get_instance(instance_name)
        payload = {"transaction_id": transaction_id}
        return await make_request(instance, "POST", "/transaction/commit", json=payload)

    @mcp.tool("rollback_transaction")
    async def rollback_transaction(
        transaction_id: str,
        instance_name: str = None
    ) -> dict:
        """
        回滚事务，恢复到事务开始前的状态。
        
        Args:
            transaction_id: begin_transaction 返回的事务ID
            instance_name: 可选的实例名称
        
        Returns:
            成功状态
        """
        instance = InstanceRegistry.get_instance(instance_name)
        payload = {"transaction_id": transaction_id}
        return await make_request(instance, "POST", "/transaction/rollback", json=payload)
