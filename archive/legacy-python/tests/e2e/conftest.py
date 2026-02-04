"""
端到端功能测试 - 配置和 fixtures
"""

import os
import pytest
import httpx
import asyncio
from pathlib import Path
from typing import AsyncGenerator


# 测试配置
BACKEND_URL = os.environ.get("BACKEND_URL", "http://localhost:5000")
SAMPLE_LIB_PATH = os.environ.get("SAMPLE_LIB_PATH", 
    str(Path(__file__).parent.parent / "fixtures" / "SampleLib" / "bin" / "Release" / "net9.0" / "SampleLib.dll"))
BACKEND_DLL_PATH = os.environ.get("BACKEND_DLL_PATH",
    str(Path(__file__).parent.parent.parent / "backend-service" / "src" / "DotNetMcp.Backend" / "bin" / "Debug" / "net9.0" / "DotNetMcp.Backend.dll"))


@pytest.fixture(scope="session")
def event_loop():
    """创建事件循环"""
    loop = asyncio.new_event_loop()
    yield loop
    loop.close()


@pytest.fixture(scope="session")
async def http_client() -> AsyncGenerator[httpx.AsyncClient, None]:
    """创建 HTTP 客户端"""
    async with httpx.AsyncClient(base_url=BACKEND_URL, timeout=30.0) as client:
        yield client


@pytest.fixture(scope="session")
def sample_lib_path() -> str:
    """获取 SampleLib.dll 路径"""
    path = Path(SAMPLE_LIB_PATH)
    if not path.exists():
        pytest.skip(f"SampleLib.dll not found at {path}")
    return str(path.absolute())


@pytest.fixture(scope="session")
def backend_dll_path() -> str:
    """获取 Backend.dll 路径（自举测试）"""
    path = Path(BACKEND_DLL_PATH)
    if not path.exists():
        pytest.skip(f"Backend DLL not found at {path}")
    return str(path.absolute())


@pytest.fixture(scope="session")
async def loaded_sample_lib(http_client: httpx.AsyncClient, sample_lib_path: str) -> dict:
    """加载 SampleLib 并返回 MVID"""
    response = await http_client.post("/assembly/load", json={"path": sample_lib_path})
    assert response.status_code == 200, f"Failed to load assembly: {response.text}"
    data = response.json()
    assert data.get("success") is True
    return data


class TestResult:
    """测试结果收集器"""
    def __init__(self):
        self.passed = []
        self.failed = []
        self.skipped = []
        self.details = {}
    
    def add_pass(self, name: str, detail: str = ""):
        self.passed.append(name)
        self.details[name] = {"status": "PASSED", "detail": detail}
    
    def add_fail(self, name: str, error: str):
        self.failed.append(name)
        self.details[name] = {"status": "FAILED", "error": error}
    
    def add_skip(self, name: str, reason: str):
        self.skipped.append(name)
        self.details[name] = {"status": "SKIPPED", "reason": reason}
    
    def summary(self) -> dict:
        return {
            "total": len(self.passed) + len(self.failed) + len(self.skipped),
            "passed": len(self.passed),
            "failed": len(self.failed),
            "skipped": len(self.skipped),
            "pass_rate": f"{len(self.passed) / max(1, len(self.passed) + len(self.failed)) * 100:.1f}%"
        }


@pytest.fixture(scope="session")
def test_results() -> TestResult:
    """测试结果收集器"""
    return TestResult()
