"""
Transaction Management Tools - MCP tools for session transactions

Tools for managing modification transactions (begin, commit, rollback).
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
        Begin a new modification transaction.
        
        Creates a snapshot of the current assembly state that can be
        restored by calling rollback_transaction.
        
        Args:
            mvid: Optional assembly MVID
            instance_name: Optional instance name.
        
        Returns:
            Transaction ID and status.
            
        Example:
            # Start a transaction
            result = begin_transaction()
            txn_id = result['data']['transaction_id']
            
            # Make modifications...
            
            # On success:
            commit_transaction(txn_id)
            
            # On failure:
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
        Commit a transaction, making all modifications permanent.
        
        Args:
            transaction_id: Transaction ID from begin_transaction
            instance_name: Optional instance name.
        
        Returns:
            Success status.
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
        Rollback a transaction, restoring assembly to pre-transaction state.
        
        Args:
            transaction_id: Transaction ID from begin_transaction
            instance_name: Optional instance name.
        
        Returns:
            Success status.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        payload = {"transaction_id": transaction_id}
        return await make_request(instance, "POST", "/transaction/rollback", json=payload)

    @mcp.tool("get_transaction")
    async def get_transaction(
        transaction_id: str,
        instance_name: str = None
    ) -> dict:
        """
        Get transaction status and details.
        
        Args:
            transaction_id: Transaction ID
            instance_name: Optional instance name.
        
        Returns:
            Transaction info including status, start time, modification count.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        return await make_request(instance, "GET", f"/transaction/{transaction_id}")

    @mcp.tool("list_transactions")
    async def list_transactions(
        instance_name: str = None
    ) -> dict:
        """
        List all transactions.
        
        Returns:
            List of all transactions with their statuses.
        """
        instance = InstanceRegistry.get_instance(instance_name)
        return await make_request(instance, "GET", "/transaction")
