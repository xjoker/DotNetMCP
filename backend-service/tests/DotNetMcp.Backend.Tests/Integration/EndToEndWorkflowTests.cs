using DotNetMcp.Backend.Core.Context;
using DotNetMcp.Backend.Core.Identity;
using DotNetMcp.Backend.Core.Compilation;
using Xunit;

namespace DotNetMcp.Backend.Tests.Integration;

/// <summary>
/// 集成测试 - 测试端到端工作流
/// </summary>
public class EndToEndWorkflowTests
{
    private readonly string _testAssemblyPath;

    public EndToEndWorkflowTests()
    {
        // 使用测试程序集本身作为测试目标
        _testAssemblyPath = typeof(EndToEndWorkflowTests).Assembly.Location;
    }

    [Fact]
    public async Task LoadAssembly_GenerateIds_ShouldWork()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);

        // Act - 加载程序集
        var loadResult = await context.LoadAsync();
        Assert.True(loadResult.IsSuccess);

        // 创建 ID 生成器
        var idGenerator = new MemberIdGenerator(context.Mvid);

        // 为第一个类型生成 ID
        var firstType = context.Assembly.MainModule.Types
            .FirstOrDefault(t => !t.Name.StartsWith("<") && t.Name != "<Module>");
        Assert.NotNull(firstType);

        var typeId = idGenerator.GenerateForType(firstType);

        // Assert
        Assert.True(MemberIdCodec.IsValid(typeId));
        Assert.True(MemberIdCodec.BelongsToAssembly(typeId, context.Mvid));

        // 解码验证
        var decoded = MemberIdCodec.Decode(typeId);
        Assert.True(decoded.IsSuccess);
        Assert.Equal(MemberKind.Type, decoded.Kind);

        // Cleanup
        context.Dispose();
    }

    [Fact]
    public async Task LoadAssembly_GetInfo_ShouldReturnValidData()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);

        // Act
        await context.LoadAsync();
        var info = context.GetInfo();

        // Assert
        Assert.NotNull(info.Name);
        Assert.NotEmpty(info.Name);
        Assert.True(info.TypesCount > 0);
        Assert.NotNull(info.Dependencies);
        Assert.NotEmpty(info.TargetFramework);

        // Cleanup
        context.Dispose();
    }

    [Fact]
    public void CompileAndAnalyze_ShouldWork()
    {
        // Arrange
        var referenceProvider = new ReferenceAssemblyProvider();
        var compilationService = new CompilationService(referenceProvider);

        var sourceCode = @"
public class TestClass
{
    public int Add(int a, int b) => a + b;
    public string Greet(string name) => $""Hello, {name}!"";
}";

        // Act - 编译
        var compileResult = compilationService.Compile(sourceCode, "TestAssembly");
        Assert.True(compileResult.IsSuccess);
        Assert.NotNull(compileResult.AssemblyBytes);

        // 将编译结果保存到临时文件
        var tempPath = Path.Combine(Path.GetTempPath(), $"TestAssembly_{Guid.NewGuid()}.dll");
        File.WriteAllBytes(tempPath, compileResult.AssemblyBytes);

        try
        {
            // 加载并分析编译后的程序集
            var context = new AssemblyContext(tempPath);
            var loadResult = context.LoadAsync().Result;
            Assert.True(loadResult.IsSuccess);

            // 生成类型 ID
            var testType = context.Assembly.MainModule.Types
                .FirstOrDefault(t => t.Name == "TestClass");
            Assert.NotNull(testType);

            var idGenerator = new MemberIdGenerator(context.Mvid);
            var typeId = idGenerator.GenerateForType(testType);
            Assert.True(MemberIdCodec.IsValid(typeId));

            // 检查方法
            Assert.Equal(2, testType.Methods.Count(m => !m.IsConstructor));

            context.Dispose();
        }
        finally
        {
            // 清理临时文件
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task FullWorkflow_LoadAnalyzeGenerateIds_ShouldComplete()
    {
        // Arrange
        var context = new AssemblyContext(_testAssemblyPath);
        await context.LoadAsync();

        var idGenerator = new MemberIdGenerator(context.Mvid);
        var generatedIds = new List<string>();

        // Act - 为所有公开类型生成 ID
        foreach (var type in context.Assembly.MainModule.Types.Where(t => t.IsPublic))
        {
            var typeId = idGenerator.GenerateForType(type);
            generatedIds.Add(typeId);

            // 为每个方法生成 ID
            foreach (var method in type.Methods.Where(m => m.IsPublic))
            {
                var methodId = idGenerator.GenerateForMethod(method);
                generatedIds.Add(methodId);
            }
        }

        // Assert
        Assert.NotEmpty(generatedIds);
        Assert.All(generatedIds, id => Assert.True(MemberIdCodec.IsValid(id)));

        // Cleanup
        context.Dispose();
    }
}
