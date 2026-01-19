"""
Instance Registry - Manage backend service instances

Singleton pattern for managing multiple backend connections.
Supports multi-user isolation with owner-based access control.
"""

import asyncio
import threading
from datetime import datetime
from typing import Dict, List, Optional

import httpx

from .config import Config, BackendInstance, make_request


class InstanceRegistry:
    """
    Backend Instance Registry (Singleton)
    
    Thread-safe implementation supporting concurrent access.
    """
    
    _instances: Dict[str, BackendInstance] = {}
    _default_instance: Optional[str] = None
    _lock = threading.RLock()
    _initialized = False
    
    @classmethod
    def initialize(cls, config: type):
        """Initialize registry from configuration."""
        with cls._lock:
            if cls._initialized:
                return
            
            # Load instances from config
            for name, instance in Config.instances.items():
                cls._instances[name] = instance
                if cls._default_instance is None:
                    cls._default_instance = name
            
            # If no instances configured, create default from backend settings
            if not cls._instances:
                default = BackendInstance(
                    name="default",
                    host=Config.backend_host,
                    port=Config.backend_port,
                    status="pending"
                )
                cls._instances["default"] = default
                cls._default_instance = "default"
            
            cls._initialized = True
    
    @classmethod
    def get_instance(cls, name: str = None) -> BackendInstance:
        """Get instance by name, or default if not specified."""
        with cls._lock:
            if name is None:
                name = cls._default_instance
            
            if name not in cls._instances:
                raise ValueError(f"Instance '{name}' not found")
            
            return cls._instances[name]
    
    @classmethod
    def list_instances(cls) -> List[BackendInstance]:
        """List all registered instances."""
        with cls._lock:
            return list(cls._instances.values())
    
    @classmethod
    def get_default_name(cls) -> Optional[str]:
        """Get default instance name."""
        return cls._default_instance
    
    @classmethod
    def set_default(cls, name: str) -> dict:
        """Set default instance."""
        with cls._lock:
            if name not in cls._instances:
                return {"success": False, "message": f"Instance '{name}' not found"}
            
            cls._default_instance = name
            return {"success": True, "message": f"Default set to '{name}'"}
    
    @classmethod
    async def add_instance(
        cls,
        host: str,
        port: int,
        name: str = None,
        token: str = None,
        owner: str = None,
        is_dynamic: bool = True
    ) -> dict:
        """Add a new backend instance."""
        with cls._lock:
            # Auto-generate name if not provided
            if name is None:
                name = f"instance-{port}"
            
            if name in cls._instances:
                return {
                    "success": False,
                    "message": f"Instance '{name}' already exists"
                }
            
            instance = BackendInstance(
                name=name,
                host=host,
                port=port,
                token=token or "",
                status="pending",
                is_dynamic=is_dynamic,
                owner=owner
            )
            
            # Test connection
            try:
                result = await make_request(instance, "GET", "/health")
                if result.get("success", True):
                    instance.status = "connected"
                else:
                    instance.status = "error"
            except Exception:
                instance.status = "error"
            
            cls._instances[name] = instance
            
            # Set as default if first instance
            if cls._default_instance is None:
                cls._default_instance = name
            
            return {
                "success": True,
                "instance": instance.to_dict()
            }
    
    @classmethod
    async def remove_instance(cls, name: str) -> dict:
        """Remove an instance."""
        with cls._lock:
            if name not in cls._instances:
                return {"success": False, "message": f"Instance '{name}' not found"}
            
            instance = cls._instances[name]
            if not instance.is_dynamic:
                return {
                    "success": False,
                    "message": "Cannot remove static instance from config"
                }
            
            del cls._instances[name]
            
            # Update default if needed
            if cls._default_instance == name:
                cls._default_instance = next(iter(cls._instances), None)
            
            return {"success": True, "message": f"Instance '{name}' removed"}
    
    @classmethod
    async def health_check_all(cls) -> dict:
        """Health check all instances."""
        results = {}
        for name, instance in cls._instances.items():
            try:
                result = await make_request(instance, "GET", "/health")
                instance.status = "connected" if result.get("success", True) else "error"
                results[name] = {"status": instance.status}
            except Exception as e:
                instance.status = "error"
                results[name] = {"status": "error", "error": str(e)}
        
        return {"instances": results}
