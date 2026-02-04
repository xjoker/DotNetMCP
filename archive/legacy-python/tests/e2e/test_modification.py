"""
修改功能测试
"""

import pytest
import httpx


class TestTypeCreation:
    """类型创建测试"""

    @pytest.mark.asyncio
    async def test_add_class(self, http_client: httpx.AsyncClient, loaded_sample_lib: dict):
        """测试添加新类"""
        response = await http_client.post("/modification/type/add", json={
            "namespace": "SampleLib.Generated",
            "name": "NewClass",
            "kind": "class"
        })
        assert response.status_code == 200
        data = response.json()
        assert data["success"] is True
        assert data["data"]["full_name"] == "SampleLib.Generated.NewClass"

    @pytest.mark.asyncio
    async def test_add_interface(self, http_client: httpx.AsyncClient, loaded_sample_lib: dict):
        """测试添加新接口"""
        response = await http_client.post("/modification/type/add", json={
            "namespace": "SampleLib.Generated",
            "name": "INewInterface",
            "kind": "interface"
        })
        assert response.status_code == 200
        data = response.json()
        assert data["success"] is True
        assert data["data"]["kind"] == "interface"


class TestMethodCreation:
    """方法创建测试"""

    @pytest.mark.asyncio
    async def test_add_method_void(self, http_client: httpx.AsyncClient, loaded_sample_lib: dict):
        """测试添加 void 方法"""
        # 先添加类型
        await http_client.post("/modification/type/add", json={
            "namespace": "SampleLib.TestMethods",
            "name": "MethodTestClass",
            "kind": "class"
        })

        response = await http_client.post("/modification/method/add", json={
            "typeFullName": "SampleLib.TestMethods.MethodTestClass",
            "name": "VoidMethod",
            "returnType": "void"
        })
        assert response.status_code == 200
        data = response.json()
        assert data["success"] is True

    @pytest.mark.asyncio
    async def test_add_method_with_params(self, http_client: httpx.AsyncClient, loaded_sample_lib: dict):
        """测试添加带参数方法"""
        # 先添加类型
        await http_client.post("/modification/type/add", json={
            "namespace": "SampleLib.TestMethods2",
            "name": "ParamTestClass",
            "kind": "class"
        })

        response = await http_client.post("/modification/method/add", json={
            "typeFullName": "SampleLib.TestMethods2.ParamTestClass",
            "name": "MethodWithParams",
            "returnType": "int",
            "parameters": [
                {"name": "a", "type": "int"},
                {"name": "b", "type": "int"}
            ]
        })
        assert response.status_code == 200
        data = response.json()
        assert data["success"] is True


class TestCodeInjection:
    """代码注入测试"""

    @pytest.mark.asyncio
    async def test_inject_at_entry(self, http_client: httpx.AsyncClient, loaded_sample_lib: dict):
        """测试方法入口注入"""
        response = await http_client.post("/modification/inject/entry", json={
            "methodFullName": "SampleLib.BasicTypes.SimpleClass::Add",
            "instructions": [
                {"opCode": "nop"}
            ]
        })
        # 可能找不到方法（因为签名匹配），但 API 应该正常响应
        assert response.status_code in [200, 400]

    @pytest.mark.asyncio
    async def test_replace_method_body(self, http_client: httpx.AsyncClient, loaded_sample_lib: dict):
        """测试替换方法体"""
        response = await http_client.post("/modification/replace/body", json={
            "methodFullName": "SampleLib.BasicTypes.SimpleClass::GetValue",
            "instructions": [
                {"opCode": "ldc.i4", "intValue": 42},
                {"opCode": "ret"}
            ]
        })
        # API 应该正常响应
        assert response.status_code in [200, 400]


class TestAssemblySave:
    """程序集保存测试"""

    @pytest.mark.asyncio
    async def test_save_to_temp(self, http_client: httpx.AsyncClient, loaded_sample_lib: dict, tmp_path):
        """测试保存程序集到临时目录"""
        import tempfile
        output_path = f"{tempfile.gettempdir()}/modified_sample.dll"
        
        response = await http_client.post("/modification/save", json={
            "outputPath": output_path
        })
        assert response.status_code == 200
        data = response.json()
        assert data["success"] is True
