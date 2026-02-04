"""
分析功能测试
"""

import pytest
import httpx


class TestAssemblyLoad:
    """程序集加载测试"""

    @pytest.mark.asyncio
    async def test_load_sample_lib(self, http_client: httpx.AsyncClient, sample_lib_path: str):
        """测试加载 SampleLib.dll"""
        response = await http_client.post("/assembly/load", json={"path": sample_lib_path})
        assert response.status_code == 200
        data = response.json()
        assert data["success"] is True
        assert "mvid" in data
        assert data["name"] == "SampleLib"

    @pytest.mark.asyncio
    async def test_load_nonexistent_assembly(self, http_client: httpx.AsyncClient):
        """测试加载不存在的程序集"""
        response = await http_client.post("/assembly/load", json={"path": "/nonexistent/path.dll"})
        assert response.status_code == 400
        data = response.json()
        assert data["success"] is False

    @pytest.mark.asyncio
    async def test_get_assembly_info(self, http_client: httpx.AsyncClient, loaded_sample_lib: dict):
        """测试获取程序集信息"""
        response = await http_client.get("/assembly/info")
        assert response.status_code == 200
        data = response.json()
        assert "name" in data or "Name" in data


class TestHealthCheck:
    """健康检查测试"""

    @pytest.mark.asyncio
    async def test_health_endpoint(self, http_client: httpx.AsyncClient):
        """测试健康检查端点"""
        response = await http_client.get("/health")
        assert response.status_code == 200
        data = response.json()
        assert data["success"] is True
        assert "service" in data


class TestRootEndpoint:
    """根端点测试"""

    @pytest.mark.asyncio
    async def test_root_endpoint(self, http_client: httpx.AsyncClient):
        """测试根端点"""
        response = await http_client.get("/")
        assert response.status_code == 200
        data = response.json()
        assert "service" in data
        assert data["status"] == "running"
