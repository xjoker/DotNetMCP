"""
Configuration Loader - Load configuration from TOML files

Priority: Environment Variables > Config File > Defaults
"""

import os
import sys
from pathlib import Path
from typing import Optional

if sys.version_info >= (3, 11):
    import tomllib
else:
    import tomli as tomllib

from .config import Config, BackendInstance


def find_config_file() -> Optional[Path]:
    """Find configuration file in standard locations."""
    search_paths = [
        Path("data/config/server.toml"),
        Path("./server.toml"),
        Path.home() / ".config" / "dotnetmcp" / "server.toml",
        Path("/app/data/config/server.toml"),  # Docker
    ]
    
    for path in search_paths:
        if path.exists():
            return path
    
    return None


def load_config() -> Config:
    """Load configuration from file and environment."""
    config_path = find_config_file()
    
    if config_path:
        with open(config_path, "rb") as f:
            data = tomllib.load(f)
        _apply_toml_config(data)
    
    # Override with environment variables
    _apply_env_overrides()
    
    return Config


def _apply_toml_config(data: dict):
    """Apply TOML configuration to Config singleton."""
    # Server section
    server = data.get("server", {})
    Config.transport = server.get("transport", Config.transport)
    Config.port = server.get("port", Config.port)
    Config.log_level = server.get("log_level", Config.log_level)
    
    # Backend section
    backend = data.get("backend", {})
    Config.backend_host = backend.get("host", Config.backend_host)
    Config.backend_port = backend.get("port", Config.backend_port)
    Config.connect_timeout = backend.get("connect_timeout", Config.connect_timeout)
    Config.health_check_interval = backend.get("health_check_interval", Config.health_check_interval)
    
    # Security section
    security = data.get("security", {})
    Config.allow_dynamic_instances = security.get("allow_dynamic_instances", False)
    
    # Users
    for user in data.get("users", []):
        Config.users[user["name"]] = {
            "token": user.get("token", ""),
            "is_admin": user.get("is_admin", False)
        }
    
    # Assembly instances
    for inst in data.get("assembly_instances", []):
        instance = BackendInstance(
            name=inst["name"],
            host=inst.get("host", Config.backend_host),
            port=inst.get("port", Config.backend_port),
            token=inst.get("token", ""),
            is_dynamic=False
        )
        Config.instances[instance.name] = instance


def _apply_env_overrides():
    """Apply environment variable overrides."""
    if os.getenv("DOTNETMCP_TRANSPORT"):
        Config.transport = os.getenv("DOTNETMCP_TRANSPORT")
    
    if os.getenv("DOTNETMCP_PORT"):
        Config.port = int(os.getenv("DOTNETMCP_PORT"))
    
    if os.getenv("DOTNETMCP_LOG_LEVEL"):
        Config.log_level = os.getenv("DOTNETMCP_LOG_LEVEL")
    
    if os.getenv("DOTNETMCP_BACKEND_HOST"):
        Config.backend_host = os.getenv("DOTNETMCP_BACKEND_HOST")
    
    if os.getenv("DOTNETMCP_BACKEND_PORT"):
        Config.backend_port = int(os.getenv("DOTNETMCP_BACKEND_PORT"))
    
    if os.getenv("DOTNETMCP_ALLOW_DYNAMIC_INSTANCES"):
        Config.allow_dynamic_instances = os.getenv("DOTNETMCP_ALLOW_DYNAMIC_INSTANCES").lower() == "true"
