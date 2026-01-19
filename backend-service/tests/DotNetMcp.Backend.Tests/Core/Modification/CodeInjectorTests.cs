using DotNetMcp.Backend.Core.Modification;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace DotNetMcp.Backend.Tests.Core.Modification;

public class CodeInjectorTests
{
    [Fact]
    public void InjectAtMethodEntry_ShouldAddInstructionsAtStart()
    {
        // Arrange
        var module = ModuleDefinition.CreateModule("Test", ModuleKind.Dll);
        var method = CreateTestMethod(module);
        var processor = method.Body.GetILProcessor();

        // 添加一些现有指令
        processor.Append(processor.Create(OpCodes.Ldc_I4_0));
        processor.Append(processor.Create(OpCodes.Ret));

        var injector = new CodeInjector(module);
        var instructions = new[] { processor.Create(OpCodes.Nop) };

        // Act
        var result = injector.InjectAtMethodEntry(method, instructions);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, method.Body.Instructions.Count);
        Assert.Equal(OpCodes.Nop, method.Body.Instructions[0].OpCode);
    }

    [Fact]
    public void InjectBeforeReturn_ShouldAddInstructionsBeforeRet()
    {
        // Arrange
        var module = ModuleDefinition.CreateModule("Test", ModuleKind.Dll);
        var method = CreateTestMethod(module);
        var processor = method.Body.GetILProcessor();

        processor.Append(processor.Create(OpCodes.Ldc_I4_0));
        processor.Append(processor.Create(OpCodes.Ret));

        var injector = new CodeInjector(module);
        var instructions = new[] { processor.Create(OpCodes.Pop), processor.Create(OpCodes.Ldc_I4_1) };

        // Act
        var result = injector.InjectBeforeReturn(method, instructions);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(4, method.Body.Instructions.Count);
        Assert.Equal(OpCodes.Ret, method.Body.Instructions[^1].OpCode);
    }

    [Fact]
    public void ReplaceMethodBody_ShouldClearAndAddNewInstructions()
    {
        // Arrange
        var module = ModuleDefinition.CreateModule("Test", ModuleKind.Dll);
        var method = CreateTestMethod(module);
        var processor = method.Body.GetILProcessor();

        // 添加旧指令
        processor.Append(processor.Create(OpCodes.Nop));
        processor.Append(processor.Create(OpCodes.Nop));
        processor.Append(processor.Create(OpCodes.Ret));

        var injector = new CodeInjector(module);
        var newInstructions = new[]
        {
            processor.Create(OpCodes.Ldc_I4_S, (sbyte)42),
            processor.Create(OpCodes.Ret)
        };

        // Act
        var result = injector.ReplaceMethodBody(method, newInstructions);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, method.Body.Instructions.Count);
    }

    private static MethodDefinition CreateTestMethod(ModuleDefinition module)
    {
        var method = new MethodDefinition("Test", MethodAttributes.Public, module.ImportReference(typeof(int)));
        method.Body = new MethodBody(method);
        return method;
    }
}
