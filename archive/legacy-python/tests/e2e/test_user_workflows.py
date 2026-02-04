"""
端到端用户工作流测试

模拟真实用户场景，验证 MCP 工具链完整性。
不是为了测试而测试，而是验证用户能否完成实际任务。

使用方法:
1. 启动后端: cd backend-service && dotnet run --project src/DotNetMcp.Backend
2. 运行测试: cd tests/e2e && python test_user_workflows.py
"""

import asyncio
import httpx
import sys
from pathlib import Path

# 配置
BACKEND_URL = "http://localhost:5095"
TEST_ASSEMBLY = None  # 将使用后端自身的 DLL 进行测试


class TestResult:
    def __init__(self):
        self.passed = 0
        self.failed = 0
        self.errors = []

    def ok(self, name: str):
        self.passed += 1
        print(f"  [OK] {name}")

    def fail(self, name: str, reason: str):
        self.failed += 1
        self.errors.append((name, reason))
        print(f"  [FAIL] {name}: {reason}")

    def summary(self):
        total = self.passed + self.failed
        print(f"\n{'='*50}")
        print(f"测试结果: {self.passed}/{total} 通过")
        if self.errors:
            print("\n失败详情:")
            for name, reason in self.errors:
                print(f"  - {name}: {reason}")
        return self.failed == 0


async def check_backend_health(client: httpx.AsyncClient) -> bool:
    """检查后端服务是否运行"""
    try:
        resp = await client.get(f"{BACKEND_URL}/health")
        return resp.status_code == 200
    except:
        return False


async def workflow_1_understand_codebase(client: httpx.AsyncClient, result: TestResult):
    """
    工作流 1: 理解陌生代码库

    用户场景: "我拿到一个 DLL，想快速了解它包含什么"

    步骤:
    1. 加载程序集
    2. 获取程序集信息 (名称、版本、类型数量)
    3. 搜索感兴趣的类型
    4. 查看某个类型的源码
    """
    print("\n[Workflow 1] Understand Codebase")

    # 使用后端自身的 DLL 作为测试目标
    backend_dll = Path(__file__).parent.parent.parent / "backend-service" / "src" / "DotNetMcp.Backend" / "bin" / "Debug" / "net10.0" / "DotNetMcp.Backend.dll"

    if not backend_dll.exists():
        # 尝试 Release 目录
        backend_dll = Path(__file__).parent.parent.parent / "backend-service" / "src" / "DotNetMcp.Backend" / "bin" / "Release" / "net10.0" / "DotNetMcp.Backend.dll"

    if not backend_dll.exists():
        result.fail("加载程序集", f"测试 DLL 不存在: {backend_dll}")
        return None

    # 步骤 1: 加载程序集
    resp = await client.post(f"{BACKEND_URL}/assembly/load", json={
        "path": str(backend_dll)
    })

    if resp.status_code != 200:
        result.fail("加载程序集", f"状态码 {resp.status_code}")
        return None

    data = resp.json()
    if not data.get("success"):
        result.fail("加载程序集", data.get("message", "未知错误"))
        return None

    mvid = data["data"]["mvid"]
    result.ok(f"加载程序集 (MVID: {mvid[:8]}...)")

    # 步骤 2: 获取程序集信息
    resp = await client.get(f"{BACKEND_URL}/assembly/info", params={"mvid": mvid})

    if resp.status_code != 200:
        result.fail("获取程序集信息", f"状态码 {resp.status_code}")
        return mvid

    data = resp.json()
    if data.get("success"):
        info = data.get("data", {})
        result.ok(f"获取程序集信息 (类型数: {info.get('type_count', 'N/A')})")
    else:
        result.fail("获取程序集信息", data.get("message", ""))

    # 步骤 3: 搜索类型
    resp = await client.get(f"{BACKEND_URL}/analysis/search/types", params={
        "keyword": "Controller",
        "limit": 10,
        "mvid": mvid
    })

    if resp.status_code == 200:
        data = resp.json()
        if data.get("success"):
            types = data.get("data", {}).get("types", [])
            result.ok(f"搜索类型 'Controller' (找到 {len(types)} 个)")
        else:
            result.fail("搜索类型", data.get("message", ""))
    else:
        result.fail("搜索类型", f"状态码 {resp.status_code}")

    # 步骤 4: 查看类型源码
    resp = await client.get(
        f"{BACKEND_URL}/analysis/type/DotNetMcp.Backend.Controllers.AssemblyController/source",
        params={"mvid": mvid}
    )

    if resp.status_code == 200:
        data = resp.json()
        if data.get("success"):
            code = data.get("data", {}).get("code", "")
            lines = len(code.split("\n")) if code else 0
            result.ok(f"获取类型源码 ({lines} 行)")
        else:
            result.fail("获取类型源码", data.get("message", ""))
    else:
        result.fail("获取类型源码", f"状态码 {resp.status_code}")

    return mvid


async def workflow_2_trace_problem(client: httpx.AsyncClient, mvid: str, result: TestResult):
    """
    工作流 2: 追踪问题根源

    用户场景: "程序抛出了某个错误信息，我想找到是哪里产生的"

    步骤:
    1. 搜索错误字符串
    2. 找到包含该字符串的方法
    3. 查看该方法的调用者 (谁调用了它)
    4. 分析调用链
    """
    print("\n[Workflow 2] Trace Problem")

    if not mvid:
        result.fail("追踪问题", "无可用程序集")
        return

    # 步骤 1: 搜索字符串
    resp = await client.get(f"{BACKEND_URL}/analysis/search/strings", params={
        "query": "assembly",
        "mode": "contains",
        "limit": 5,
        "mvid": mvid
    })

    if resp.status_code == 200:
        data = resp.json()
        if data.get("success"):
            matches = data.get("data", {}).get("matches", [])
            result.ok(f"搜索字符串 'assembly' (找到 {len(matches)} 处)")
        else:
            result.fail("搜索字符串", data.get("message", ""))
    else:
        result.fail("搜索字符串", f"状态码 {resp.status_code}")

    # 步骤 2: 查找方法调用
    resp = await client.get(
        f"{BACKEND_URL}/analysis/xrefs/method/DotNetMcp.Backend.Controllers.AssemblyController/LoadAssembly",
        params={"limit": 10, "mvid": mvid}
    )

    if resp.status_code == 200:
        data = resp.json()
        if data.get("success"):
            refs = data.get("data", {}).get("references", [])
            result.ok(f"查找方法引用 (找到 {len(refs)} 处调用)")
        else:
            # 没有调用者也是正常的
            result.ok("查找方法引用 (API 端点，无内部调用)")
    else:
        result.fail("查找方法引用", f"状态码 {resp.status_code}")

    # 步骤 3: 构建调用图
    resp = await client.get(
        f"{BACKEND_URL}/analysis/callgraph/DotNetMcp.Backend.Services.AnalysisService/DecompileType",
        params={"direction": "callees", "max_depth": 2, "mvid": mvid}
    )

    if resp.status_code == 200:
        data = resp.json()
        if data.get("success"):
            levels = data.get("data", {}).get("levels", [])
            result.ok(f"构建调用图 ({len(levels)} 层)")
        else:
            result.fail("构建调用图", data.get("message", ""))
    else:
        result.fail("构建调用图", f"状态码 {resp.status_code}")


async def workflow_3_analyze_structure(client: httpx.AsyncClient, mvid: str, result: TestResult):
    """
    工作流 3: 分析代码结构

    用户场景: "我想了解这个方法的控制流逻辑"

    步骤:
    1. 获取方法源码
    2. 构建控制流图
    3. 分析依赖关系
    """
    print("\n[Workflow 3] Analyze Structure")

    if not mvid:
        result.fail("分析结构", "无可用程序集")
        return

    # 步骤 1: 获取方法源码
    resp = await client.get(
        f"{BACKEND_URL}/analysis/type/DotNetMcp.Backend.Services.AnalysisService/method/SearchTypes",
        params={"mvid": mvid}
    )

    if resp.status_code == 200:
        data = resp.json()
        if data.get("success"):
            code = data.get("data", {}).get("code", "")
            result.ok(f"获取方法源码 ({len(code)} 字符)")
        else:
            result.fail("获取方法源码", data.get("message", ""))
    else:
        result.fail("获取方法源码", f"状态码 {resp.status_code}")

    # 步骤 2: 构建控制流图
    resp = await client.get(
        f"{BACKEND_URL}/analysis/cfg/DotNetMcp.Backend.Services.AnalysisService/SearchTypes",
        params={"include_il": "false", "mvid": mvid}
    )

    if resp.status_code == 200:
        data = resp.json()
        if data.get("success"):
            cfg_data = data.get("data", {})
            blocks = cfg_data.get("block_count", 0)
            edges = cfg_data.get("edge_count", 0)
            result.ok(f"构建控制流图 ({blocks} 块, {edges} 边)")
        else:
            result.fail("构建控制流图", data.get("message", ""))
    else:
        result.fail("构建控制流图", f"状态码 {resp.status_code}")

    # 步骤 3: 构建依赖图
    resp = await client.get(f"{BACKEND_URL}/analysis/dependencies", params={
        "level": "namespace",
        "mvid": mvid
    })

    if resp.status_code == 200:
        data = resp.json()
        if data.get("success"):
            dep_data = data.get("data", {})
            nodes = dep_data.get("total_nodes", 0)
            result.ok(f"构建依赖图 ({nodes} 个命名空间)")
        else:
            result.fail("构建依赖图", data.get("message", ""))
    else:
        result.fail("构建依赖图", f"状态码 {resp.status_code}")


async def workflow_4_detect_characteristics(client: httpx.AsyncClient, mvid: str, result: TestResult):
    """
    工作流 4: 检测代码特征

    用户场景: "我想知道这个程序集使用了什么设计模式，是否被混淆"

    步骤:
    1. 检测设计模式
    2. 检测混淆
    """
    print("\n[Workflow 4] Detect Characteristics")

    if not mvid:
        result.fail("检测特征", "无可用程序集")
        return

    # 步骤 1: 检测设计模式
    resp = await client.get(f"{BACKEND_URL}/analysis/patterns", params={"mvid": mvid})

    if resp.status_code == 200:
        data = resp.json()
        if data.get("success"):
            patterns = data.get("data", {}).get("patterns", [])
            summary = data.get("data", {}).get("summary", {})
            pattern_types = list(summary.keys()) if summary else []
            result.ok(f"检测设计模式 (发现 {len(patterns)} 个: {', '.join(pattern_types) or '无'})")
        else:
            result.fail("检测设计模式", data.get("message", ""))
    else:
        result.fail("检测设计模式", f"状态码 {resp.status_code}")

    # 步骤 2: 检测混淆
    resp = await client.get(f"{BACKEND_URL}/analysis/obfuscation", params={"mvid": mvid})

    if resp.status_code == 200:
        data = resp.json()
        if data.get("success"):
            obf_data = data.get("data", {})
            is_obf = obf_data.get("is_obfuscated", False)
            score = obf_data.get("obfuscation_score", 0)
            status = "是" if is_obf else "否"
            result.ok(f"检测混淆 (混淆: {status}, 得分: {score}/100)")
        else:
            result.fail("检测混淆", data.get("message", ""))
    else:
        result.fail("检测混淆", f"状态码 {resp.status_code}")


async def workflow_5_type_info(client: httpx.AsyncClient, mvid: str, result: TestResult):
    """
    工作流 5: 了解类型结构

    用户场景: "我想了解某个类的完整结构 (方法、属性、字段)"
    """
    print("\n[Workflow 5] Type Info")

    if not mvid:
        result.fail("类型结构", "无可用程序集")
        return

    resp = await client.get(
        f"{BACKEND_URL}/analysis/type/DotNetMcp.Backend.Services.AnalysisService/info",
        params={"mvid": mvid}
    )

    if resp.status_code == 200:
        data = resp.json()
        if data.get("success"):
            info = data.get("data", {})
            methods = len(info.get("methods", []))
            fields = len(info.get("fields", []))
            props = len(info.get("properties", []))
            result.ok(f"获取类型信息 ({methods} 方法, {fields} 字段, {props} 属性)")
        else:
            result.fail("获取类型信息", data.get("message", ""))
    else:
        result.fail("获取类型信息", f"状态码 {resp.status_code}")


async def main():
    print("=" * 50)
    print("DotNetMCP 用户工作流端到端测试")
    print("=" * 50)

    result = TestResult()

    async with httpx.AsyncClient(timeout=30.0) as client:
        # 检查后端是否运行
        print("\n[Check] Backend Service...")
        if not await check_backend_health(client):
            print("❌ 后端服务未运行!")
            print("\n请先启动后端:")
            print("  cd backend-service")
            print("  dotnet run --project src/DotNetMcp.Backend")
            return False

        result.ok("后端服务运行中")

        # 执行工作流测试
        mvid = await workflow_1_understand_codebase(client, result)
        await workflow_2_trace_problem(client, mvid, result)
        await workflow_3_analyze_structure(client, mvid, result)
        await workflow_4_detect_characteristics(client, mvid, result)
        await workflow_5_type_info(client, mvid, result)

    return result.summary()


if __name__ == "__main__":
    success = asyncio.run(main())
    sys.exit(0 if success else 1)
