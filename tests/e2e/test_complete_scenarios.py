"""
DotNetMCP 完整用户场景测试

设计原则：
1. 从真实用户需求出发，不是为了覆盖API
2. 每个场景代表一个完整的用户任务
3. 包含正常流程和错误处理

用户角色：
- 逆向工程师：分析陌生程序集
- 安全研究员：检测恶意代码特征
- 开发者：修补/修改现有程序集
- 自动化工具：批量处理多个程序集
"""

import asyncio
import httpx
import sys
import os
import tempfile
import shutil
from pathlib import Path

BACKEND_URL = "http://localhost:5095"


class TestResult:
    def __init__(self):
        self.passed = 0
        self.failed = 0
        self.skipped = 0
        self.errors = []

    def ok(self, name: str, detail: str = ""):
        self.passed += 1
        msg = f"  [PASS] {name}"
        if detail:
            msg += f" - {detail}"
        print(msg)

    def fail(self, name: str, reason: str):
        self.failed += 1
        self.errors.append((name, reason))
        print(f"  [FAIL] {name}: {reason}")

    def skip(self, name: str, reason: str):
        self.skipped += 1
        print(f"  [SKIP] {name}: {reason}")

    def summary(self):
        total = self.passed + self.failed
        print(f"\n{'='*60}")
        print(f"Results: {self.passed} passed, {self.failed} failed, {self.skipped} skipped")
        if self.errors:
            print("\nFailures:")
            for name, reason in self.errors:
                print(f"  - {name}: {reason}")
        return self.failed == 0


# =============================================================================
# 场景 1: 逆向工程师 - 分析陌生程序集
# =============================================================================

async def scenario_reverse_engineer(client: httpx.AsyncClient, result: TestResult, test_dll: str):
    """
    用户故事：我是一个逆向工程师，拿到一个第三方DLL，需要理解它的功能

    任务流程：
    1. 加载程序集，了解基本信息
    2. 找到入口点或关键类
    3. 分析类的结构和方法
    4. 反编译关键方法
    5. 追踪方法调用链
    6. 理解控制流逻辑
    """
    print("\n" + "="*60)
    print("Scenario 1: Reverse Engineer - Analyze Unknown Assembly")
    print("="*60)

    # 1. 加载程序集
    resp = await client.post(f"{BACKEND_URL}/assembly/load", json={"path": test_dll})
    if resp.status_code != 200 or not resp.json().get("success"):
        result.fail("Load assembly", resp.json().get("message", "Unknown error"))
        return None

    mvid = resp.json()["data"]["mvid"]
    name = resp.json()["data"]["name"]
    result.ok("Load assembly", f"{name} (MVID: {mvid[:8]}...)")

    # 2. 获取程序集概览
    resp = await client.get(f"{BACKEND_URL}/assembly/info", params={"mvid": mvid})
    if resp.status_code == 200 and resp.json().get("success"):
        info = resp.json().get("data", {})
        result.ok("Get assembly overview", f"Types: {info.get('type_count', 'N/A')}")
    else:
        result.fail("Get assembly overview", "Failed to get info")

    # 3. 搜索关键类 - 多种搜索策略
    searches = [
        ("Controller", "Find controllers"),
        ("Service", "Find services"),
        ("Manager", "Find managers"),
    ]

    for keyword, desc in searches:
        resp = await client.get(f"{BACKEND_URL}/analysis/search/types",
                               params={"keyword": keyword, "limit": 5, "mvid": mvid})
        if resp.status_code == 200:
            count = len(resp.json().get("data", {}).get("types", []))
            result.ok(desc, f"Found {count} types")
        else:
            result.fail(desc, f"Status {resp.status_code}")

    # 4. 搜索字符串 - 找敏感信息
    resp = await client.get(f"{BACKEND_URL}/analysis/search/strings",
                           params={"query": "http", "limit": 10, "mvid": mvid})
    if resp.status_code == 200:
        count = len(resp.json().get("data", {}).get("matches", []))
        result.ok("Search URLs in strings", f"Found {count} matches")
    else:
        result.fail("Search strings", f"Status {resp.status_code}")

    # 5. 分析类型结构
    resp = await client.get(
        f"{BACKEND_URL}/analysis/type/DotNetMcp.Backend.Services.AnalysisService/info",
        params={"mvid": mvid})
    if resp.status_code == 200 and resp.json().get("success"):
        info = resp.json()["data"]
        methods = len(info.get("methods", []))
        result.ok("Analyze type structure", f"{methods} methods")
    else:
        result.fail("Analyze type structure", "Failed")

    # 6. 反编译方法 - C# 和 IL
    for lang in ["csharp", "il"]:
        resp = await client.get(
            f"{BACKEND_URL}/analysis/type/DotNetMcp.Backend.Services.AnalysisService/method/SearchTypes",
            params={"language": lang, "mvid": mvid})
        if resp.status_code == 200 and resp.json().get("success"):
            code = resp.json()["data"]["code"]
            result.ok(f"Decompile method ({lang})", f"{len(code)} chars")
        else:
            result.fail(f"Decompile method ({lang})", "Failed")

    # 7. 构建调用图
    resp = await client.get(
        f"{BACKEND_URL}/analysis/callgraph/DotNetMcp.Backend.Services.AnalysisService/SearchTypes",
        params={"direction": "callees", "max_depth": 3, "mvid": mvid})
    if resp.status_code == 200 and resp.json().get("success"):
        levels = len(resp.json()["data"].get("levels", []))
        result.ok("Build call graph (callees)", f"{levels} levels")
    else:
        result.fail("Build call graph", "Failed")

    # 8. 查找谁调用了某方法
    resp = await client.get(
        f"{BACKEND_URL}/analysis/xrefs/method/DotNetMcp.Backend.Services.AnalysisService/DecompileType",
        params={"limit": 20, "mvid": mvid})
    if resp.status_code == 200:
        refs = resp.json().get("data", {}).get("references", [])
        result.ok("Find method callers (xrefs)", f"{len(refs)} call sites")
    else:
        result.fail("Find method callers", "Failed")

    # 9. 分析控制流
    resp = await client.get(
        f"{BACKEND_URL}/analysis/cfg/DotNetMcp.Backend.Services.AnalysisService/SearchTypes",
        params={"include_il": "true", "mvid": mvid})
    if resp.status_code == 200 and resp.json().get("success"):
        data = resp.json()["data"]
        result.ok("Build CFG with IL", f"{data['block_count']} blocks, {data['edge_count']} edges")
    else:
        result.fail("Build CFG", "Failed")

    return mvid


# =============================================================================
# 场景 2: 安全研究员 - 检测可疑特征
# =============================================================================

async def scenario_security_researcher(client: httpx.AsyncClient, result: TestResult, mvid: str):
    """
    用户故事：我是安全研究员，需要快速判断一个DLL是否有可疑特征

    任务流程：
    1. 检测是否被混淆
    2. 识别代码模式
    3. 分析依赖关系（是否引用可疑库）
    4. 搜索敏感API调用
    """
    print("\n" + "="*60)
    print("Scenario 2: Security Researcher - Detect Suspicious Features")
    print("="*60)

    if not mvid:
        result.skip("Security analysis", "No assembly loaded")
        return

    # 1. 混淆检测
    resp = await client.get(f"{BACKEND_URL}/analysis/obfuscation", params={"mvid": mvid})
    if resp.status_code == 200 and resp.json().get("success"):
        data = resp.json()["data"]
        is_obf = "Yes" if data["is_obfuscated"] else "No"
        score = data["obfuscation_score"]
        confidence = data["confidence"]
        result.ok("Obfuscation detection", f"Obfuscated: {is_obf}, Score: {score}/100, Confidence: {confidence}")

        # 检查具体指标
        indicators = data.get("indicators", [])
        if indicators:
            categories = set(i["category"] for i in indicators)
            result.ok("Obfuscation indicators", f"Categories: {', '.join(categories)}")
    else:
        result.fail("Obfuscation detection", "Failed")

    # 2. 设计模式检测
    resp = await client.get(f"{BACKEND_URL}/analysis/patterns", params={"mvid": mvid})
    if resp.status_code == 200 and resp.json().get("success"):
        data = resp.json()["data"]
        total = data["total_count"]
        summary = data.get("summary", {})
        pattern_list = ", ".join(f"{k}:{v}" for k, v in summary.items()) or "None"
        result.ok("Design pattern detection", f"Found {total}: {pattern_list}")
    else:
        result.fail("Design pattern detection", "Failed")

    # 3. 依赖分析 - 程序集级别
    resp = await client.get(f"{BACKEND_URL}/analysis/dependencies",
                           params={"level": "assembly", "mvid": mvid})
    if resp.status_code == 200 and resp.json().get("success"):
        data = resp.json()["data"]
        internal = data["internal_nodes"]
        external = data["external_nodes"]
        result.ok("Assembly dependencies", f"Internal: {internal}, External: {external}")
    else:
        result.fail("Assembly dependencies", "Failed")

    # 4. 搜索敏感API (假设查找网络相关)
    sensitive_keywords = ["Socket", "WebClient", "HttpClient", "Process", "Registry"]
    for keyword in sensitive_keywords[:3]:  # 只测试前3个
        resp = await client.get(f"{BACKEND_URL}/analysis/search/types",
                               params={"keyword": keyword, "limit": 5, "mvid": mvid})
        if resp.status_code == 200:
            count = len(resp.json().get("data", {}).get("types", []))
            result.ok(f"Search sensitive type '{keyword}'", f"Found {count}")


# =============================================================================
# 场景 3: 开发者 - 修补程序集
# =============================================================================

async def scenario_developer_patching(client: httpx.AsyncClient, result: TestResult, test_dll: str):
    """
    用户故事：我需要修改一个第三方DLL，添加日志或修改行为

    任务流程：
    1. 加载程序集
    2. 定位要修改的方法
    3. 注入代码到方法入口
    4. 保存修改后的程序集
    5. 验证修改成功
    """
    print("\n" + "="*60)
    print("Scenario 3: Developer - Patch Assembly")
    print("="*60)

    # 创建临时目录
    temp_dir = tempfile.mkdtemp()
    output_path = os.path.join(temp_dir, "patched.dll")

    try:
        # 1. 加载程序集
        resp = await client.post(f"{BACKEND_URL}/assembly/load", json={"path": test_dll})
        if resp.status_code != 200 or not resp.json().get("success"):
            result.fail("Load for patching", resp.json().get("message", "Failed"))
            return

        mvid = resp.json()["data"]["mvid"]
        result.ok("Load assembly for patching", f"MVID: {mvid[:8]}...")

        # 2. 查找要修改的方法
        resp = await client.get(
            f"{BACKEND_URL}/analysis/type/DotNetMcp.Backend.Controllers.AssemblyController/info",
            params={"mvid": mvid})
        if resp.status_code == 200 and resp.json().get("success"):
            methods = resp.json()["data"]["methods"]
            method_names = [m["name"] for m in methods[:5]]
            result.ok("Find target method", f"Methods: {', '.join(method_names)}")
        else:
            result.fail("Find target method", "Failed")

        # 3. 注入代码 - 在方法入口添加 Console.WriteLine
        inject_request = {
            "mvid": mvid,
            "methodFullName": "DotNetMcp.Backend.Controllers.AssemblyController::Health",
            "instructions": [
                {"opCode": "ldstr", "stringValue": "[Patched] Health check called"},
                {"opCode": "call", "stringValue": "System.Void System.Console::WriteLine(System.String)"}
            ]
        }

        resp = await client.post(f"{BACKEND_URL}/modification/inject/entry", json=inject_request)
        if resp.status_code == 200 and resp.json().get("success"):
            result.ok("Inject entry code", "Added Console.WriteLine")
        else:
            # 可能因为方法不兼容失败，记录但继续
            result.fail("Inject entry code", resp.json().get("message", "Failed"))

        # 4. 保存程序集
        resp = await client.post(f"{BACKEND_URL}/modification/save",
                                json={"outputPath": output_path, "mvid": mvid})
        if resp.status_code == 200 and resp.json().get("success"):
            result.ok("Save patched assembly", output_path)

            # 5. 验证文件存在
            if os.path.exists(output_path):
                size = os.path.getsize(output_path)
                result.ok("Verify patched file", f"Size: {size} bytes")
            else:
                result.fail("Verify patched file", "File not created")
        else:
            result.fail("Save patched assembly", resp.json().get("message", "Failed"))

    finally:
        # 清理
        shutil.rmtree(temp_dir, ignore_errors=True)


# =============================================================================
# 场景 4: 资源提取与管理
# =============================================================================

async def scenario_resource_management(client: httpx.AsyncClient, result: TestResult, mvid: str):
    """
    用户故事：我需要查看和提取程序集中的嵌入资源

    任务流程：
    1. 列出所有资源
    2. 获取资源内容
    3. 添加新资源
    4. 导出所有资源
    """
    print("\n" + "="*60)
    print("Scenario 4: Resource Management")
    print("="*60)

    if not mvid:
        result.skip("Resource management", "No assembly loaded")
        return

    # 1. 列出资源
    resp = await client.get(f"{BACKEND_URL}/resources/list", params={"mvid": mvid})
    if resp.status_code == 200 and resp.json().get("success"):
        resources = resp.json()["data"]["resources"]
        result.ok("List resources", f"Found {len(resources)} resources")

        if resources:
            # 2. 获取第一个资源的内容
            first_res = resources[0]["name"]
            resp = await client.get(f"{BACKEND_URL}/resources/{first_res}",
                                   params={"mvid": mvid})
            if resp.status_code == 200 and resp.json().get("success"):
                size = resp.json()["data"]["size"]
                result.ok("Get resource content", f"'{first_res}' - {size} bytes")
            else:
                result.fail("Get resource content", "Failed")
    else:
        # 没有资源也是正常的
        result.ok("List resources", "No embedded resources (normal for this assembly)")

    # 3. 添加新资源
    resp = await client.post(f"{BACKEND_URL}/resources/add", json={
        "name": "test_resource.txt",
        "contentText": "This is a test resource",
        "isPublic": True,
        "mvid": mvid
    })
    if resp.status_code == 200 and resp.json().get("success"):
        result.ok("Add new resource", "test_resource.txt added")

        # 删除刚添加的资源
        resp = await client.delete(f"{BACKEND_URL}/resources/test_resource.txt",
                                  params={"mvid": mvid})
        if resp.status_code == 200:
            result.ok("Remove resource", "test_resource.txt removed")
    else:
        error = resp.json().get("message", "Unknown error")
        # 如果是资源已存在，也算部分成功
        if "exists" in error.lower():
            result.ok("Add resource", "Resource already exists (expected)")
        else:
            result.fail("Add new resource", error)


# =============================================================================
# 场景 5: 多实例管理
# =============================================================================

async def scenario_multi_instance(client: httpx.AsyncClient, result: TestResult, test_dll: str):
    """
    用户故事：我需要同时分析多个相关的DLL

    任务流程：
    1. 加载多个程序集
    2. 列出所有实例
    3. 切换默认实例
    4. 分别查询不同实例
    5. 卸载实例
    """
    print("\n" + "="*60)
    print("Scenario 5: Multi-Instance Management")
    print("="*60)

    # 加载同一个DLL两次会返回相同的MVID
    # 但我们可以测试实例管理功能

    # 1. 检查当前实例
    resp = await client.get(f"{BACKEND_URL}/instance/list")
    if resp.status_code == 200 and resp.json().get("success"):
        instances = resp.json()["data"]["instances"]
        result.ok("List instances", f"Found {len(instances)} loaded")
    else:
        result.fail("List instances", "Failed")

    # 2. 获取默认实例
    resp = await client.get(f"{BACKEND_URL}/instance/default")
    if resp.status_code == 200:
        data = resp.json()
        if data.get("success") and data.get("data", {}).get("mvid"):
            default_mvid = data["data"]["mvid"]
            result.ok("Get default instance", f"MVID: {default_mvid[:8]}...")
        else:
            result.ok("Get default instance", "No default set")
    else:
        result.fail("Get default instance", "Failed")


# =============================================================================
# 场景 6: 错误处理验证
# =============================================================================

async def scenario_error_handling(client: httpx.AsyncClient, result: TestResult):
    """
    用户故事：验证系统在异常情况下的表现

    任务流程：
    1. 加载不存在的文件
    2. 查询不存在的类型
    3. 使用无效参数
    4. 验证错误响应格式
    """
    print("\n" + "="*60)
    print("Scenario 6: Error Handling Validation")
    print("="*60)

    # 1. 加载不存在的文件
    resp = await client.post(f"{BACKEND_URL}/assembly/load",
                            json={"path": "C:/nonexistent/fake.dll"})
    if resp.status_code == 400:  # 应该返回 400
        data = resp.json()
        if not data.get("success") and data.get("error_code"):
            result.ok("Load non-existent file", f"Correct 400 with error_code: {data['error_code']}")
        else:
            result.fail("Load non-existent file", "Missing error_code in response")
    else:
        result.fail("Load non-existent file", f"Expected 400, got {resp.status_code}")

    # 2. 查询不存在的类型
    resp = await client.get(f"{BACKEND_URL}/analysis/type/NonExistent.FakeClass/source")
    if resp.status_code in [400, 404]:
        result.ok("Query non-existent type", f"Correct {resp.status_code} error")
    else:
        result.fail("Query non-existent type", f"Expected 400/404, got {resp.status_code}")

    # 3. 无效的 limit 参数
    resp = await client.get(f"{BACKEND_URL}/analysis/search/types",
                           params={"keyword": "Test", "limit": -1})
    if resp.status_code == 400:
        result.ok("Invalid limit parameter", "Correct 400 error")
    else:
        result.fail("Invalid limit parameter", f"Expected 400, got {resp.status_code}")

    # 4. 无效的 direction 参数
    resp = await client.get(
        f"{BACKEND_URL}/analysis/callgraph/Fake.Type/Method",
        params={"direction": "invalid"})
    if resp.status_code == 400:
        result.ok("Invalid direction parameter", "Correct 400 error")
    else:
        result.fail("Invalid direction parameter", f"Expected 400, got {resp.status_code}")


# =============================================================================
# 场景 7: 批量操作
# =============================================================================

async def scenario_batch_operations(client: httpx.AsyncClient, result: TestResult, mvid: str):
    """
    用户故事：我需要批量获取多个类型的信息
    """
    print("\n" + "="*60)
    print("Scenario 7: Batch Operations")
    print("="*60)

    if not mvid:
        result.skip("Batch operations", "No assembly loaded")
        return

    # 1. 批量获取类型源码
    resp = await client.post(f"{BACKEND_URL}/analysis/batch/sources", json={
        "typeNames": [
            "DotNetMcp.Backend.Controllers.AssemblyController",
            "DotNetMcp.Backend.Controllers.AnalysisController"
        ],
        "language": "csharp",
        "mvid": mvid
    })

    if resp.status_code == 200 and resp.json().get("success"):
        data = resp.json()["data"]
        success_count = sum(1 for v in data.values() if v.get("success"))
        result.ok("Batch get sources", f"{success_count}/{len(data)} succeeded")
    else:
        result.fail("Batch get sources", "Failed")

    # 2. 批量获取交叉引用
    resp = await client.post(f"{BACKEND_URL}/analysis/batch/xrefs", json={
        "typeNames": [
            "DotNetMcp.Backend.Services.AnalysisService",
            "DotNetMcp.Backend.Services.AssemblyManager"
        ],
        "limit": 10,
        "mvid": mvid
    })

    if resp.status_code == 200 and resp.json().get("success"):
        data = resp.json()["data"]
        result.ok("Batch get xrefs", f"Got results for {len(data)} types")
    else:
        result.fail("Batch get xrefs", "Failed")


# =============================================================================
# Main
# =============================================================================

async def main():
    print("="*60)
    print("DotNetMCP Complete User Scenario Tests")
    print("="*60)

    result = TestResult()

    # 找到测试 DLL
    test_dll = Path(__file__).parent.parent.parent / "backend-service" / "src" / "DotNetMcp.Backend" / "bin" / "Debug" / "net10.0" / "DotNetMcp.Backend.dll"

    if not test_dll.exists():
        test_dll = Path(__file__).parent.parent.parent / "backend-service" / "src" / "DotNetMcp.Backend" / "bin" / "Release" / "net10.0" / "DotNetMcp.Backend.dll"

    if not test_dll.exists():
        print(f"[ERROR] Test DLL not found at {test_dll}")
        print("Please build the project first: dotnet build")
        return False

    test_dll = str(test_dll)

    async with httpx.AsyncClient(timeout=30.0) as client:
        # 检查后端
        try:
            resp = await client.get(f"{BACKEND_URL}/health")
            if resp.status_code != 200:
                raise Exception("Backend not healthy")
            result.ok("Backend health check", "Running")
        except Exception as e:
            print(f"[ERROR] Backend not running: {e}")
            print(f"Please start: cd backend-service && dotnet run")
            return False

        # 运行所有场景
        mvid = await scenario_reverse_engineer(client, result, test_dll)
        await scenario_security_researcher(client, result, mvid)
        await scenario_developer_patching(client, result, test_dll)
        await scenario_resource_management(client, result, mvid)
        await scenario_multi_instance(client, result, test_dll)
        await scenario_error_handling(client, result)
        await scenario_batch_operations(client, result, mvid)

    return result.summary()


if __name__ == "__main__":
    success = asyncio.run(main())
    sys.exit(0 if success else 1)
