"""
Configuration Module - Server configuration management

Handles loading configuration from TOML files and environment variables.
"""

import os
from dataclasses import dataclass, field
from typing import Optional, Dict, Any

import httpx


@dataclass
class BackendInstance:
    """Backend service instance configuration."""
    name: str
    host: str
    port: int
    token: str = ""
    status: str = "pending"
    is_dynamic: bool = False
    owner: Optional[str] = None
    
    @property
    def url(self) -> str:
        return f"http://{self.host}:{self.port}"
    
    def to_dict(self) -> dict:
        return {
            "name": self.name,
            "host": self.host,
            "port": self.port,
            "url": self.url,
            "status": self.status,
            "is_dynamic": self.is_dynamic,
            "owner": self.owner
        }


class Config:
    """Global configuration singleton."""
    
    # Server settings
    transport: str = "http"
    port: int = 8651
    log_level: str = "info"
    
    # Backend settings
    backend_host: str = "127.0.0.1"
    backend_port: int = 8650
    connect_timeout: float = 10.0
    health_check_interval: int = 30
    
    # Security settings
    allow_dynamic_instances: bool = False
    
    # Users
    users: Dict[str, dict] = field(default_factory=dict)
    
    # Instances
    instances: Dict[str, BackendInstance] = field(default_factory=dict)


# HTTP client for backend communication
_http_client: Optional[httpx.AsyncClient] = None


def get_http_client() -> httpx.AsyncClient:
    """Get or create HTTP client for backend requests."""
    global _http_client
    if _http_client is None:
        _http_client = httpx.AsyncClient(timeout=Config.connect_timeout)
    return _http_client


async def make_request(
    instance: BackendInstance,
    method: str,
    path: str,
    params: dict = None,
    json: dict = None
) -> dict:
    """Make HTTP request to backend service."""
    client = get_http_client()
    url = f"{instance.url}{path}"
    
    headers = {}
    if instance.token:
        headers["Authorization"] = f"Bearer {instance.token}"
    
    try:
        response = await client.request(
            method=method,
            url=url,
            params=params,
            json=json,
            headers=headers
        )
        response.raise_for_status()
        return response.json()
    except httpx.HTTPStatusError as e:
        return {
            "success": False,
            "error": f"HTTP {e.response.status_code}",
            "message": e.response.text
        }
    except httpx.RequestError as e:
        return {
            "success": False,
            "error": "ConnectionError",
            "message": str(e)
        }
